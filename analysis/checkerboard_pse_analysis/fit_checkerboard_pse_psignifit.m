% Single cumulative Gaussian fit for the checkerboard experiment
% using psignifit 4.
%
% We fit P(response = Convex) as a function of visual_space_l.
% The PSE is the l-value at which Concave and Convex are equally likely.

% Optional: set csv_path in the workspace before running this script.
% Otherwise choose a trials.csv file. Never silently analyse an old pilot.
clearvars -except csv_path psignifit_path; close all; clc;


%% settings

if ~exist('csv_path', 'var') || strlength(string(csv_path)) == 0
    project_root = fileparts(fileparts(fileparts(mfilename('fullpath'))));
    [file_name, folder_name] = uigetfile( ...
        fullfile(project_root, 'measurements', '*_trials.csv'), ...
        'Choose the checkerboard trials CSV');
    if isequal(file_name, 0)
        disp('No file selected. Analysis cancelled.');
        return;
    end
    csv_path = fullfile(folder_name, file_name);
end
csv_path = string(csv_path);
fprintf('input file: %s\n', csv_path);

% Change this only if the psignifit folder is moved.
if ~exist('psignifit_path', 'var')
    psignifit_path = "C:\Users\ZVSL-070\Downloads\psignifit-matlab\psignifit-master";
end
if isfolder(psignifit_path)
    addpath(genpath(psignifit_path));
end


%% load data

T = readtable(csv_path);
fprintf('rows in CSV: %d\n', height(T));
required_columns = {'response', 'valid_for_analysis', 'visual_space_l', ...
    'participant_id', 'eye_presentation', 'angular_diameter_deg'};
if ~all(ismember(required_columns, T.Properties.VariableNames))
    error('This is not a checkerboard trials CSV. Required columns are missing.');
end

% Keep only valid trials with a Concave or Convex response.
valid_response = strcmpi(string(T.response), "Concave") | ...
                 strcmpi(string(T.response), "Convex");
T = T(T.valid_for_analysis == 1 & valid_response, :);

fprintf('valid trials used: %d\n', height(T));

if isempty(T)
    error('No valid Concave/Convex trials were found.');
end

% A single fit must not mix people, sessions, eyes, FOVs, mappings or sequences.
% Only visual_space_l is allowed to vary within this fit.
condition_columns = {'participant_id', 'session_start_utc', 'eye_presentation', ...
    'angular_diameter_deg', 'mapping_version', 'trial_sequence', ...
    'grid_line_spacing_deg', 'aperture_edge_softness_deg', 'circular_aperture_enabled'};
for column = condition_columns
    name = column{1};
    if ismember(name, T.Properties.VariableNames) && numel(unique(string(T.(name)))) > 1
        error('Mixed condition in column %s. Select one condition before fitting.', name);
    end
end
if any(~isfinite(T.visual_space_l))
    error('Stimulus values contain NaN or Inf. Check the input CSV.');
end
if ismember('sequence_index', T.Properties.VariableNames) ...
        && numel(unique(T.sequence_index)) ~= height(T)
    error('Duplicate valid trials (sequence_index). Check the input CSV.');
end


%% prepare responses

% Convex is the positive response (1); Concave is 0.
T.convex = double(strcmpi(string(T.response), "Convex"));

if numel(unique(T.convex)) < 2
    error(['Only one response category is present. A PSE cannot be ', ...
           'estimated without both Concave and Convex responses.']);
end

[l_values, n_convex, n_trials, prop_convex] = aggregateResponses(T);
if numel(l_values) < 3
    error('Fewer than three stimulus levels. Add levels before fitting a pilot PSE.');
end

fprintf('\naggregated data:\n');
for k = 1:length(l_values)
    fprintf('  l=%.3f  convex=%d/%d  P=%.3f\n', ...
        l_values(k), n_convex(k), n_trials(k), prop_convex(k));
end

if min(prop_convex) > 0.5 || max(prop_convex) < 0.5
    warning(['The measured responses do not cross 50%%. ', ...
             'The PSE may be outside the tested l-range and unreliable.']);
end

% psignifit data format: [stimulus level, positive responses, all trials]
data = [l_values(:), n_convex(:), n_trials(:)];


%% choose increasing or decreasing curve

mean_l_convex = mean(T.visual_space_l(T.convex == 1));
mean_l_concave = mean(T.visual_space_l(T.convex == 0));

if mean_l_convex >= mean_l_concave
    sigmoid_name = 'norm';
    direction_name = 'increasing';
else
    sigmoid_name = 'neg_norm';
    direction_name = 'decreasing';
end

fprintf('\nThe fitted P(Convex) function is %s.\n', direction_name);


%% cumulative Gaussian fit with psignifit

if exist('psignifit', 'file') == 0
    error(['psignifit was not found. Check psignifit_path at the ', ...
           'beginning of this script.']);
end

options = struct;
options.sigmoidName = sigmoid_name;       % cumulative Gaussian
options.expType = 'equalAsymptote';       % equal error rate at both ends
options.threshPC = 0.5;                   % PSE at 50% Convex
options.confP = 0.95;                     % 95% credible intervals

result = psignifit(data, options);


%% get PSE, JND, lapse rate, and credible intervals

% psignifit parameters: [threshold, width, lambda, gamma, eta]
pse = result.Fit(1);
width = result.Fit(2);                    % distance from 5% to 95%
lapse_rate = result.Fit(3);               % same as gamma in this model
eta = result.Fit(5);                      % extra response variability

% For a cumulative Gaussian, the JND is half the 25%-to-75% interval.
% psignifit's width is the 5%-to-95% interval, so it is converted here.
jnd_factor = 0.67448975 / 3.28970725;
jnd = width * jnd_factor;

% result.conf_Intervals contains [lower, upper] for each parameter.
pse_ci95 = squeeze(result.conf_Intervals(1, :, 1));
width_ci95 = squeeze(result.conf_Intervals(2, :, 1));
lapse_ci95 = squeeze(result.conf_Intervals(3, :, 1));
eta_ci95 = squeeze(result.conf_Intervals(5, :, 1));
jnd_ci95 = width_ci95 * jnd_factor;
if pse_ci95(1) < min(l_values) || pse_ci95(2) > max(l_values)
    warning(['The 95%% credible interval extends beyond the tested range. ', ...
        'Do not interpret this PSE as a well-covered neutral point.']);
end


%% print results

participant_id = string(T.participant_id(1));

fprintf('\n----\n');
fprintf('Results for %s (psignifit)\n', participant_id);
fprintf('----\n');
fprintf('PSE                    = %.4f\n', pse);
fprintf('PSE 95%% credible int. = [%.4f, %.4f]\n', pse_ci95(1), pse_ci95(2));
fprintf('PSE - straight l=1     = %.4f\n', pse - 1);
fprintf('JND                    = %.4f\n', jnd);
fprintf('JND 95%% credible int. = [%.4f, %.4f]\n', jnd_ci95(1), jnd_ci95(2));
fprintf('symmetric lapse rate   = %.4f\n', lapse_rate);
fprintf('eta                    = %.4f\n', eta);


%% save result as Excel file

result_table = table( ...
    participant_id, height(T), pse, pse_ci95(1), pse_ci95(2), pse - 1, ...
    jnd, jnd_ci95(1), jnd_ci95(2), width, ...
    lapse_rate, lapse_ci95(1), lapse_ci95(2), ...
    eta, eta_ci95(1), eta_ci95(2), ...
    'VariableNames', { ...
    'participant_id', 'n_trials', 'pse_visual_space_l', ...
    'pse_ci95_low', 'pse_ci95_high', 'pse_minus_straight_l1', ...
    'jnd_l', 'jnd_ci95_low', 'jnd_ci95_high', 'width_5_to_95', ...
    'symmetric_lapse_rate', 'lapse_ci95_low', 'lapse_ci95_high', ...
    'eta', 'eta_ci95_low', 'eta_ci95_high'});
% Keep the source and condition with the fitted value, not only the person's ID.
result_table.source_csv = csv_path;
result_table.eye_presentation = string(T.eye_presentation(1));
result_table.angular_diameter_deg = T.angular_diameter_deg(1);
result_table.sigmoid_name = string(sigmoid_name);
result_table.n_stimulus_levels = numel(l_values);

data_folder = fileparts(csv_path);
result_file = fullfile(data_folder, 'checkerboard_pse_result_psignifit.xlsx');
writetable(result_table, result_file);
fprintf('result saved: %s\n', result_file);


%% plot

figure('Position', [100 100 900 600]);

plot_options = struct;
plot_options.dataColor = [0 0 0.55];
plot_options.lineColor = [1 0.4 0];
plot_options.lineWidth = 2;
plot_options.xLabel = 'Visual-space parameter l';
plot_options.yLabel = 'P(response = Convex)';
plot_options.plotPar = false;
plot_options.CIthresh = true;

[h_fit, h_data] = plotPsych(result, plot_options);
hold on;

xline(pse, ':', 'Color', [1 0.4 0], 'LineWidth', 1.5, ...
    'HandleVisibility', 'off');
h_straight = xline(1, '--', 'Color', [0.5 0.5 0.5]);
yline(0.5, ':', 'Color', [0.5 0.5 0.5], ...
    'HandleVisibility', 'off');

title({'Checkerboard psychometric function (psignifit)', ...
       char(participant_id)}, ...
       'Interpreter', 'none');
grid on;
h_ci = plot(nan, nan, '--', 'Color', [1 0.4 0]);
legend([h_data(1), h_fit, h_straight, h_ci], ...
    {'data', sprintf('fit (PSE = %.3f)', pse), ...
     'l = 1 (geometrically straight)', ...
     sprintf('95%% credible interval [%.3f, %.3f]', pse_ci95(1), pse_ci95(2))}, ...
    'Location', 'best');

safe_id = regexprep(char(participant_id), '[^A-Za-z0-9_-]', '_');
plot_file = fullfile(data_folder, ...
    sprintf('checkerboard_psychometric_psignifit_%s.png', safe_id));
saveas(gcf, plot_file);
fprintf('plot saved: %s\n', plot_file);


%% helper function

function [l_values, n_convex, n_trials, prop_convex] = aggregateResponses(T)
    l_values = unique(T.visual_space_l)';
    n_convex = zeros(size(l_values));
    n_trials = zeros(size(l_values));

    for k = 1:length(l_values)
        sub = T(T.visual_space_l == l_values(k), :);
        n_convex(k) = sum(sub.convex);
        n_trials(k) = height(sub);
    end

    prop_convex = n_convex ./ n_trials;
end

% Single cumulative Gaussian fit for checkerboard OR random-dot trials
% using psignifit 4.
%
% We fit P(response = Convex) against visual_space_l (checkerboard)
% or instrument_distortion_k (random dots). These are NOT equated or converted.
% The PSE is the stimulus value with 50% Convex responses.

% Optional: set csv_path in the workspace before running this script.
% Otherwise choose a trials.csv file. Never silently analyse an old pilot.
% Optional condition_filter struct selects one condition from a mixed CSV:
% condition_filter = struct('motion_mode', 'SimulatedYaw', 'angular_diameter_deg', 60);
clearvars -except csv_path psignifit_path condition_filter; close all; clc;


%% settings

if ~exist('csv_path', 'var') || strlength(string(csv_path)) == 0
    project_root = fileparts(fileparts(fileparts(mfilename('fullpath'))));
    [file_name, folder_name] = uigetfile( ...
        fullfile(project_root, 'measurements', '*_trials.csv'), ...
        'Choose the checkerboard or random-dot trials CSV');
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
required_columns = {'response', 'valid_for_analysis', ...
    'participant_id', 'eye_presentation', 'angular_diameter_deg'};
if ~all(ismember(required_columns, T.Properties.VariableNames))
    error('This is not a supported trials CSV. Required columns are missing.');
end

has_l = ismember('visual_space_l', T.Properties.VariableNames);
has_k = ismember('instrument_distortion_k', T.Properties.VariableNames);
if has_l == has_k
    error('CSV must contain exactly one stimulus column: visual_space_l or instrument_distortion_k.');
end
if has_k
    random_dot_columns = {'motion_mode', 'instrument_magnification_m', 'content_zoom', 'sweep_axis'};
    if ~all(ismember(random_dot_columns, T.Properties.VariableNames))
        error('Random-dot CSV is missing motion/magnification/zoom/axis columns needed to separate conditions.');
    end
    task_name = 'Random-dot';
    output_prefix = 'random_dot';
    stimulus_name = 'k';
    stimulus_column = 'instrument_distortion_k';
    x_label = 'Instrument distortion parameter k';
    reference_label = 'k = 1 (tangent condition)';
    pse_column = 'pse_instrument_distortion_k';
    jnd_column = 'jnd_k';
    reference_column = 'pse_minus_tangent_k1';
else
    task_name = 'Checkerboard';
    output_prefix = 'checkerboard';
    stimulus_name = 'l';
    stimulus_column = 'visual_space_l';
    x_label = 'Visual-space parameter l';
    reference_label = 'l = 1 (geometrically straight)';
    pse_column = 'pse_visual_space_l';
    jnd_column = 'jnd_l';
    reference_column = 'pse_minus_straight_l1';
end
fprintf('detected task: %s; stimulus column: %s\n', task_name, stimulus_column);

if exist('condition_filter', 'var') && ~isempty(condition_filter)
    if ~isstruct(condition_filter) || ~isscalar(condition_filter)
        error('condition_filter must be a scalar struct.');
    end
    for filter_name = string(fieldnames(condition_filter))'
        name = char(filter_name);
        if ~ismember(name, T.Properties.VariableNames)
            error('Unknown filter column: %s.', name);
        end
        wanted = condition_filter.(name);
        if numel(string(wanted)) ~= 1
            error('Each condition_filter field must select exactly one value.');
        end
        if isnumeric(T.(name)) && isnumeric(wanted)
            keep = abs(T.(name) - wanted) <= 1e-8 * max(1, abs(wanted));
        else
            keep = strcmpi(string(T.(name)), string(wanted));
        end
        T = T(keep, :);
    end
end
T.stimulus_value = T.(stimulus_column);

% Keep only valid trials with a Concave or Convex response.
valid_response = strcmpi(string(T.response), "Concave") | ...
                 strcmpi(string(T.response), "Convex");
T = T(T.valid_for_analysis == 1 & valid_response, :);

fprintf('valid trials used: %d\n', height(T));

if isempty(T)
    error('No valid Concave/Convex trials were found.');
end

% A single fit must not mix people, sessions, eyes, FOVs, mappings or sequences.
% Only the selected stimulus parameter may vary; seeds/directions may vary by trial.
condition_columns = {'participant_id', 'session_start_utc', 'eye_presentation', ...
    'angular_diameter_deg', 'mapping_version', 'trial_sequence', ...
    'grid_line_spacing_deg', 'aperture_edge_softness_deg', 'circular_aperture_enabled'};
if has_k
    condition_columns = [condition_columns, {'motion_mode', 'instrument_magnification_m', ...
        'content_zoom', 'sweep_axis', 'sweep_speed_deg_per_s', ...
        'image_center_speed_deg_per_s', 'head_motion_constraint', 'carrier_radius_m'}];
end
for column = condition_columns
    name = column{1};
    if ismember(name, T.Properties.VariableNames)
        values = string(T.(name));
        % Free head movement can leave a target speed unspecified in every row.
        % Treat repeated missing entries as one condition, not different values.
        values(ismissing(values)) = "<unspecified>";
        if numel(unique(values)) > 1
            error('Mixed condition in column %s. Select one condition before fitting.', name);
        end
    end
end
if any(~isfinite(T.stimulus_value))
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

[stimulus_values, n_convex, n_trials, prop_convex] = aggregateResponses(T);
if numel(stimulus_values) < 3
    error('Fewer than three stimulus levels. Add levels before fitting a pilot PSE.');
end

fprintf('\naggregated data:\n');
for k = 1:length(stimulus_values)
    fprintf('  %s=%.3f  convex=%d/%d  P=%.3f\n', ...
        stimulus_name, stimulus_values(k), n_convex(k), n_trials(k), prop_convex(k));
end

if min(prop_convex) > 0.5 || max(prop_convex) < 0.5
    warning(['The measured responses do not cross 50%%. ', ...
             'The PSE may be outside the tested stimulus range and unreliable.']);
end

% psignifit data format: [stimulus level, positive responses, all trials]
data = [stimulus_values(:), n_convex(:), n_trials(:)];


%% choose increasing or decreasing curve

mean_stimulus_convex = mean(T.stimulus_value(T.convex == 1));
mean_stimulus_concave = mean(T.stimulus_value(T.convex == 0));

if mean_stimulus_convex >= mean_stimulus_concave
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
if pse_ci95(1) < min(stimulus_values) || pse_ci95(2) > max(stimulus_values)
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
fprintf('PSE minus reference 1 = %.4f (%s)\n', pse - 1, reference_label);
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
    'participant_id', 'n_trials', pse_column, ...
    'pse_ci95_low', 'pse_ci95_high', reference_column, ...
    jnd_column, 'jnd_ci95_low', 'jnd_ci95_high', 'width_5_to_95', ...
    'symmetric_lapse_rate', 'lapse_ci95_low', 'lapse_ci95_high', ...
    'eta', 'eta_ci95_low', 'eta_ci95_high'});
% Keep the source and condition with the fitted value, not only the person's ID.
result_table.source_csv = csv_path;
result_table.eye_presentation = string(T.eye_presentation(1));
result_table.angular_diameter_deg = T.angular_diameter_deg(1);
result_table.sigmoid_name = string(sigmoid_name);
result_table.n_stimulus_levels = numel(stimulus_values);
result_table.task = string(task_name);
result_table.stimulus_parameter = string(stimulus_column);
for column = condition_columns
    name = column{1};
    if ismember(name, T.Properties.VariableNames) && ~ismember(name, result_table.Properties.VariableNames)
        result_table.(name) = T.(name)(1);
    end
end

data_folder = fileparts(csv_path);
% Include the selected condition so two fits of one old mixed CSV cannot overwrite each other.
condition_suffix = '';
if has_k
    suffix_columns = {'motion_mode', 'mode'; 'angular_diameter_deg', 'fov'; ...
        'instrument_magnification_m', 'm'; 'content_zoom', 'zoom'; ...
        'sweep_axis', 'axis'; 'image_center_speed_deg_per_s', 'speed'; ...
        'head_motion_constraint', 'head'; 'eye_presentation', 'eye'};
    for index = 1:size(suffix_columns, 1)
        name = suffix_columns{index, 1};
        if ismember(name, T.Properties.VariableNames)
            value = string(T.(name)(1));
            if ismissing(value)
                value = "unspecified";
            end
            value = regexprep(char(value), '[^A-Za-z0-9_-]', '_');
            condition_suffix = [condition_suffix, '_', suffix_columns{index, 2}, value]; %#ok<AGROW>
        end
    end
end
result_file = fullfile(data_folder, [output_prefix, '_pse_result_psignifit', condition_suffix, '.xlsx']);
writetable(result_table, result_file);
fprintf('result saved: %s\n', result_file);


%% plot

figure('Position', [100 100 900 600]);

plot_options = struct;
plot_options.dataColor = [0 0 0.55];
plot_options.lineColor = [1 0.4 0];
plot_options.lineWidth = 2;
plot_options.xLabel = x_label;
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

plot_subtitle = participant_id;
if has_k
    plot_subtitle = sprintf('%s | %s | m = %g | FOV = %g deg', participant_id, ...
        string(T.motion_mode(1)), T.instrument_magnification_m(1), T.angular_diameter_deg(1));
end
title({[task_name, ' psychometric function (psignifit)'], ...
       char(plot_subtitle)}, ...
       'Interpreter', 'none');
grid on;
h_ci = plot(nan, nan, '--', 'Color', [1 0.4 0]);
legend([h_data(1), h_fit, h_straight, h_ci], ...
    {'data', sprintf('fit (PSE = %.3f)', pse), ...
     reference_label, ...
     sprintf('95%% credible interval [%.3f, %.3f]', pse_ci95(1), pse_ci95(2))}, ...
    'Location', 'best');

safe_id = regexprep(char(participant_id), '[^A-Za-z0-9_-]', '_');
plot_file = fullfile(data_folder, ...
    sprintf('%s_psychometric_psignifit_%s%s.png', output_prefix, safe_id, condition_suffix));
saveas(gcf, plot_file);
fprintf('plot saved: %s\n', plot_file);


%% helper function

function [stimulus_values, n_convex, n_trials, prop_convex] = aggregateResponses(T)
    stimulus_values = unique(T.stimulus_value)';
    n_convex = zeros(size(stimulus_values));
    n_trials = zeros(size(stimulus_values));

    for k = 1:length(stimulus_values)
        sub = T(T.stimulus_value == stimulus_values(k), :);
        n_convex(k) = sum(sub.convex);
        n_trials(k) = height(sub);
    end

    prop_convex = n_convex ./ n_trials;
end

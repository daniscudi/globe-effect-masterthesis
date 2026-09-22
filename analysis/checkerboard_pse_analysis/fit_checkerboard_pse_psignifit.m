% Single cumulative Gaussian fit for the checkerboard experiment
% using psignifit 4.
%
% We fit P(response = Convex) as a function of visual_space_l.
% The PSE is the l-value at which Concave and Convex are equally likely.

clear; close all; clc;


%% settings

% Change this path for each new participant.
csv_path = "D:\Tolga\Globe-Effect-Master\VRCheckerboard\measurements\20260904_154429_checkerboard_pilot\20260904_154429_checkerboard_pilot\pilot_ys_checkerboard_pilot_20260904_154429_trials.csv";

% Change this only if the psignifit folder is moved.
psignifit_path = "C:\Users\ZVSL-070\Downloads\psignifit-matlab\psignifit-master";
addpath(genpath(psignifit_path));


%% load data

T = readtable(csv_path);
fprintf('rows in CSV: %d\n', height(T));

% Keep only valid trials with a Concave or Convex response.
valid_response = strcmpi(string(T.response), "Concave") | ...
                 strcmpi(string(T.response), "Convex");
T = T(T.valid_for_analysis == 1 & valid_response, :);

fprintf('valid trials used: %d\n', height(T));

if isempty(T)
    error('No valid Concave/Convex trials were found.');
end

% The fit should contain only one eye, FOV, and zoom condition.
n_eyes = numel(unique(string(T.eye_presentation)));
n_fovs = numel(unique(T.angular_diameter_deg));
n_zooms = numel(unique(T.content_zoom));

if n_eyes > 1 || n_fovs > 1 || n_zooms > 1
    error(['The CSV contains several eye, FOV, or zoom conditions. ', ...
           'Select one condition before fitting.']);
end


%% prepare responses

% Convex is the positive response (1); Concave is 0.
T.convex = double(strcmpi(string(T.response), "Convex"));

if numel(unique(T.convex)) < 2
    error(['Only one response category is present. A PSE cannot be ', ...
           'estimated without both Concave and Convex responses.']);
end

[l_values, n_convex, n_trials, prop_convex] = aggregateResponses(T);

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
legend([h_data(1), h_fit, h_straight], ...
    {'data', sprintf('fit (PSE = %.3f)', pse), ...
     'l = 1 (geometrically straight)'}, ...
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

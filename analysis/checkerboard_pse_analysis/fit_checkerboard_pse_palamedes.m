% Single cumulative Gaussian fit for the checkerboard experiment
%
% The participant answers Concave or Convex in every trial.
% We fit P(response = Convex) as a function of visual_space_l.
% The PSE is the l-value at which both answers are equally likely (P = 0.5).

clear; close all; clc;


%% settings

% Insert the path to one *_trials.csv file from the checkerboard experiment.
csv_path = "D:\Tolga\Globe-Effect-Master\VRCheckerboard\measurements\pilot_td_checkerboard_pilot_20260918_183240_trials.csv";


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

% The current pilot normally contains only one eye, FOV, and zoom condition.
% If a later CSV contains several conditions, choose one before fitting, e.g.:
%
% T = T(strcmpi(string(T.eye_presentation), "BothEyes"), :);
% T = T(T.angular_diameter_deg == 90, :);
% T = T(T.content_zoom == 1, :);

n_eyes = numel(unique(string(T.eye_presentation)));
n_fovs = numel(unique(T.angular_diameter_deg));
n_zooms = numel(unique(T.content_zoom));

if n_eyes > 1 || n_fovs > 1 || n_zooms > 1
    error(['The CSV contains several eye, FOV, or zoom conditions. ', ...
           'Select one condition with the filter lines above before fitting.']);
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


%% choose increasing or decreasing curve

% Palamedes fits an increasing cumulative Gaussian. If Convex responses are
% more common at small l-values, we mirror the l-axis for the fit and convert
% the PSE back afterwards.
mean_l_convex = mean(T.visual_space_l(T.convex == 1));
mean_l_concave = mean(T.visual_space_l(T.convex == 0));

if mean_l_convex >= mean_l_concave
    direction = 1;
    direction_name = "increasing";
else
    direction = -1;
    direction_name = "decreasing";
end

fit_levels = direction .* l_values;
[fit_levels, order] = sort(fit_levels);
fit_n_convex = n_convex(order);
fit_n_trials = n_trials(order);

fprintf('\nP(Convex) is fitted as an %s function of l.\n', direction_name);


%% cumulative Gaussian fit with Palamedes

if exist('PAL_PFML_Fit', 'file') == 0
    error(['Palamedes is not on the MATLAB path. First use: ', ...
           'addpath(genpath(''D:\path\to\Palamedes''))']);
end

PF = @PAL_CumulativeNormal;

% Parameters: [threshold, slope, guess rate, lapse rate]
% Only threshold and slope are fitted. Guess and lapse are fixed at zero.
paramsFree = [1 1 0 0];

searchGrid.alpha = linspace(min(fit_levels), max(fit_levels), 101);
searchGrid.beta = logspace(-1, 3, 101);
searchGrid.gamma = 0;
searchGrid.lambda = 0;

[params, LL, exitflag] = PAL_PFML_Fit( ...
    fit_levels, fit_n_convex, fit_n_trials, ...
    searchGrid, paramsFree, PF);

% The threshold of this cumulative Gaussian is its 50% point.
pse = direction .* params(1);
sigma = 1 ./ params(2);
jnd = 0.67449 .* sigma; % half of the fitted 25%-to-75% interval

if exitflag ~= 1
    warning('The optimizer did not fully converge (exitflag=%d).', exitflag);
end


%% print results

participant_id = string(T.participant_id(1));

fprintf('\n----\n');
fprintf('Results for %s\n', participant_id);
fprintf('----\n');
fprintf('PSE (visual_space_l) = %.4f\n', pse);
fprintf('PSE - straight l=1   = %.4f\n', pse - 1);
fprintf('JND                   = %.4f\n', jnd);
fprintf('slope beta            = %.4f\n', params(2));
fprintf('log-likelihood        = %.4f\n', LL);


%% save result as Excel file

result = table(participant_id, height(T), pse, pse - 1, jnd, params(2), LL, ...
    'VariableNames', {'participant_id', 'n_trials', 'pse_visual_space_l', ...
    'pse_minus_straight_l1', 'jnd_l', 'beta', 'log_likelihood'});

data_folder = fileparts(csv_path);
result_file = fullfile(data_folder, 'checkerboard_pse_result.xlsx');
writetable(result, result_file);
fprintf('result saved: %s\n', result_file);


%% plot

figure('Position', [100 100 900 600]);
hold on;

plot(l_values, prop_convex, 'o', ...
    'Color', [0 0 0.55], 'MarkerFaceColor', [0 0 0.55], ...
    'MarkerSize', 9, 'DisplayName', 'data');

x_smooth = linspace(min(l_values), max(l_values), 300);
y_smooth = PF(params, direction .* x_smooth);
plot(x_smooth, y_smooth, '-', ...
    'Color', [1 0.4 0], 'LineWidth', 2, ...
    'DisplayName', sprintf('fit (PSE = %.3f)', pse));

xline(pse, ':', 'Color', [1 0.4 0], 'LineWidth', 1.5, ...
    'HandleVisibility', 'off');
xline(1, '--', 'Color', [0.5 0.5 0.5], ...
    'DisplayName', 'l = 1 (geometrically straight)');
yline(0.5, ':', 'Color', [0.5 0.5 0.5], ...
    'HandleVisibility', 'off');

xlabel('Visual-space parameter l');
ylabel('P(response = Convex)');
title({'Checkerboard psychometric function (Palamedes)', ...
       char(participant_id)}, ...
       'Interpreter', 'none');
ylim([-0.05 1.05]);
grid on;
legend('Location', 'best');

safe_id = regexprep(char(participant_id), '[^A-Za-z0-9_-]', '_');
plot_file = fullfile(data_folder, ...
    sprintf('checkerboard_psychometric_%s.png', safe_id));
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

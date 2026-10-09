% Single cumulative Gaussian fit for checkerboard OR random-dot trials
% using psignifit 4.
%
% We fit P(response = Convex) against visual_space_l (checkerboard)
% or instrument_distortion_k (random dots). These are NOT equated or converted.
% The PSE is the stimulus value with 50% Convex responses.

% Optional: set csv_path in the workspace before running this script.
% Otherwise choose a trials.csv file. Never silently analyse an old pilot.
% Older random-dot CSVs (one file with SimulatedYaw AND HeadTracked) are split
% automatically: each motion mode is fitted separately with its own XLSX/PNG.
% Settings changed during a session (FOV, m, speed, edge softness, ...) are
% pooled into one fit with a warning that lists the values.
% Optional condition_filter struct selects one condition from a mixed CSV:
% condition_filter = struct('motion_mode', 'SimulatedYaw', 'angular_diameter_deg', 60);
clearvars -except csv_path psignifit_path condition_filter; close all; clc;


%% settings

if ~exist('csv_path', 'var') || strlength(string(csv_path)) == 0
    % Filter and start folder are passed separately: a combined path is an
    % "invalid file filter" when the folder does not exist (or mfilename is
    % empty, e.g. when only a section is run).
    start_folder = fullfile(fileparts(fileparts(fileparts(mfilename('fullpath')))), 'measurements');
    if ~isfolder(start_folder)
        start_folder = pwd;
    end
    [file_name, folder_name] = uigetfile( ...
        {'*_trials.csv', 'Trials CSV (*_trials.csv)'; '*.csv', 'All CSV files (*.csv)'}, ...
        'Choose the checkerboard or random-dot trials CSV', [start_folder, filesep]);
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
    % sweep_axis is optional: the earliest pilot CSVs had horizontal sweeps only.
    random_dot_columns = {'motion_mode', 'instrument_magnification_m', 'content_zoom'};
    if ~all(ismember(random_dot_columns, T.Properties.VariableNames))
        error('Random-dot CSV is missing motion/magnification/zoom columns needed to separate conditions.');
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

% Columns that describe the condition of a fit. Only the stimulus parameter
% should vary; seeds/directions may vary by trial.
condition_columns = {'participant_id', 'session_start_utc', 'eye_presentation', ...
    'angular_diameter_deg', 'mapping_version', 'trial_sequence', ...
    'grid_line_spacing_deg', 'aperture_edge_softness_deg', 'circular_aperture_enabled'};
split_columns = {};
if has_k
    condition_columns = [condition_columns, {'motion_mode', 'instrument_magnification_m', ...
        'content_zoom', 'sweep_axis', 'sweep_speed_deg_per_s', ...
        'image_center_speed_deg_per_s', 'head_motion_constraint', 'carrier_radius_m'}];
    % Older random-dot sessions stored SimulatedYaw AND HeadTracked in one CSV.
    % These are different tasks and are always fitted separately.
    split_columns = {'motion_mode'};
end
[group_keys, group_id] = conditionGroups(T, split_columns);

% Different people or sessions in one fit are always an error. Any other
% setting changed during a session (FOV, m, speed, edge softness, ... e.g.
% while testing a pilot) is pooled into one fit with a warning; the result
% file lists the values and their trial counts.
strict_columns = {'participant_id', 'session_start_utc'};
pooled_columns = {};
for column = condition_columns
    name = column{1};
    if ~ismember(name, T.Properties.VariableNames) || ismember(name, split_columns)
        continue;
    end
    if ismember(name, strict_columns)
        if numel(unique(conditionValues(T, name))) > 1
            error('Mixed condition in column %s. Select one condition before fitting.', name);
        end
        continue;
    end
    for g = 1:numel(group_keys)
        rows = T(group_id == g, :);
        if numel(unique(conditionValues(rows, name))) > 1
            pooled_columns = union(pooled_columns, {name}, 'stable');
            warning(['Mixed values in column %s (%s): %s. These trials are pooled ', ...
                'into one fit. Use condition_filter to fit one value only.'], ...
                name, group_keys(g), valueCounts(rows, name));
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


%% split into conditions and fit each one

if exist('psignifit', 'file') == 0
    error(['psignifit was not found. Check psignifit_path at the ', ...
           'beginning of this script.']);
end

fprintf('conditions found: %d\n', numel(group_keys));
for g = 1:numel(group_keys)
    fprintf('  [%d] %s  (%d valid trials)\n', g, group_keys(g), sum(group_id == g));
end

task = struct('csv_path', csv_path, 'has_k', has_k, 'task_name', task_name, ...
    'output_prefix', output_prefix, 'stimulus_name', stimulus_name, ...
    'stimulus_column', stimulus_column, 'x_label', x_label, ...
    'reference_label', reference_label, 'pse_column', pse_column, ...
    'jnd_column', jnd_column, 'reference_column', reference_column);
task.condition_columns = condition_columns;
task.pooled_columns = pooled_columns;

all_results = table();
for g = 1:numel(group_keys)
    fprintf('\n==== condition %d/%d: %s ====\n', g, numel(group_keys), group_keys(g));
    try
        result_table = fitOneCondition(T(group_id == g, :), task);
        all_results = [all_results; result_table]; %#ok<AGROW>
    catch fit_error
        if numel(group_keys) == 1
            rethrow(fit_error);
        end
        % One weak condition of an old mixed CSV must not block the others.
        warning('Condition skipped (%s): %s', group_keys(g), fit_error.message);
    end
end

if numel(group_keys) > 1 && ~isempty(all_results)
    fprintf('\n---- summary of all conditions ----\n');
    summary_columns = intersect({'motion_mode', 'instrument_magnification_m', ...
        'angular_diameter_deg', 'n_trials', pse_column, 'pse_ci95_low', ...
        'pse_ci95_high', jnd_column}, all_results.Properties.VariableNames, 'stable');
    disp(all_results(:, summary_columns));
    summary_file = fullfile(fileparts(csv_path), [output_prefix, '_pse_summary_psignifit.xlsx']);
    writetable(all_results, summary_file);
    fprintf('summary saved: %s\n', summary_file);
end


%% helper functions

function result_table = fitOneCondition(T, task)
    % prepare responses

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
            task.stimulus_name, stimulus_values(k), n_convex(k), n_trials(k), prop_convex(k));
    end

    if min(prop_convex) > 0.5 || max(prop_convex) < 0.5
        warning(['The measured responses do not cross 50%%. ', ...
                 'The PSE may be outside the tested stimulus range and unreliable.']);
    end

    % psignifit data format: [stimulus level, positive responses, all trials]
    data = [stimulus_values(:), n_convex(:), n_trials(:)];


    % choose increasing or decreasing curve

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


    % cumulative Gaussian fit with psignifit

    options = struct;
    options.sigmoidName = sigmoid_name;       % cumulative Gaussian
    options.expType = 'equalAsymptote';       % equal error rate at both ends
    options.threshPC = 0.5;                   % PSE at 50% Convex
    options.confP = 0.95;                     % 95% credible intervals

    result = psignifit(data, options);


    % get PSE, JND, lapse rate, and credible intervals

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


    % print results

    participant_id = string(T.participant_id(1));

    fprintf('\n----\n');
    fprintf('Results for %s (psignifit)\n', participant_id);
    fprintf('----\n');
    fprintf('PSE                    = %.4f\n', pse);
    fprintf('PSE 95%% credible int. = [%.4f, %.4f]\n', pse_ci95(1), pse_ci95(2));
    fprintf('PSE minus reference 1 = %.4f (%s)\n', pse - 1, task.reference_label);
    fprintf('JND                    = %.4f\n', jnd);
    fprintf('JND 95%% credible int. = [%.4f, %.4f]\n', jnd_ci95(1), jnd_ci95(2));
    fprintf('symmetric lapse rate   = %.4f\n', lapse_rate);
    fprintf('eta                    = %.4f\n', eta);


    % save result as Excel file

    result_table = table( ...
        participant_id, height(T), pse, pse_ci95(1), pse_ci95(2), pse - 1, ...
        jnd, jnd_ci95(1), jnd_ci95(2), width, ...
        lapse_rate, lapse_ci95(1), lapse_ci95(2), ...
        eta, eta_ci95(1), eta_ci95(2), ...
        'VariableNames', { ...
        'participant_id', 'n_trials', task.pse_column, ...
        'pse_ci95_low', 'pse_ci95_high', task.reference_column, ...
        task.jnd_column, 'jnd_ci95_low', 'jnd_ci95_high', 'width_5_to_95', ...
        'symmetric_lapse_rate', 'lapse_ci95_low', 'lapse_ci95_high', ...
        'eta', 'eta_ci95_low', 'eta_ci95_high'});
    % Keep the source and condition with the fitted value, not only the person's ID.
    result_table.source_csv = task.csv_path;
    result_table.eye_presentation = resultValue(T, 'eye_presentation', task);
    result_table.angular_diameter_deg = resultValue(T, 'angular_diameter_deg', task);
    result_table.sigmoid_name = string(sigmoid_name);
    result_table.n_stimulus_levels = numel(stimulus_values);
    result_table.task = string(task.task_name);
    result_table.stimulus_parameter = string(task.stimulus_column);
    for column = task.condition_columns
        name = column{1};
        if ismember(name, T.Properties.VariableNames) && ~ismember(name, result_table.Properties.VariableNames)
            result_table.(name) = resultValue(T, name, task);
        end
    end

    data_folder = fileparts(task.csv_path);
    % Include the selected condition so two fits of one old mixed CSV cannot overwrite each other.
    condition_suffix = '';
    if task.has_k
        suffix_columns = {'motion_mode', 'mode'; 'angular_diameter_deg', 'fov'; ...
            'instrument_magnification_m', 'm'; 'content_zoom', 'zoom'; ...
            'sweep_axis', 'axis'; 'image_center_speed_deg_per_s', 'speed'; ...
            'head_motion_constraint', 'head'; 'eye_presentation', 'eye'};
        for index = 1:size(suffix_columns, 1)
            name = suffix_columns{index, 1};
            % Pooled columns are listed in the XLSX, not in the file name.
            if ismember(name, T.Properties.VariableNames) && ~ismember(name, task.pooled_columns)
                value = string(T.(name)(1));
                if ismissing(value)
                    value = "unspecified";
                end
                value = regexprep(char(value), '[^A-Za-z0-9_-]', '_');
                condition_suffix = [condition_suffix, '_', suffix_columns{index, 2}, value]; %#ok<AGROW>
            end
        end
        % Windows paths are limited to 260 characters. Deep measurement folders
        % plus a long suffix can exceed that; the XLSX still lists every value.
        longest_path = fullfile(data_folder, [task.output_prefix, '_psychometric_psignifit_', ...
            char(participant_id), condition_suffix, '.png']);
        if strlength(longest_path) > 250
            condition_suffix = ['_mode', regexprep(char(string(T.motion_mode(1))), '[^A-Za-z0-9_-]', '_')];
        end
    end
    result_file = fullfile(data_folder, [task.output_prefix, '_pse_result_psignifit', condition_suffix, '.xlsx']);
    writetable(result_table, result_file);
    fprintf('result saved: %s\n', result_file);


    % plot

    figure('Position', [100 100 900 600]);

    plot_options = struct;
    plot_options.dataColor = [0 0 0.55];
    plot_options.lineColor = [1 0.4 0];
    plot_options.lineWidth = 2;
    plot_options.xLabel = task.x_label;
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
    if task.has_k
        plot_subtitle = sprintf('%s | %s | m = %s | FOV = %s deg', participant_id, ...
            string(T.motion_mode(1)), plotValue(T, 'instrument_magnification_m', task), ...
            plotValue(T, 'angular_diameter_deg', task));
    end
    title({[task.task_name, ' psychometric function (psignifit)'], ...
           char(plot_subtitle)}, ...
           'Interpreter', 'none');
    grid on;
    h_ci = plot(nan, nan, '--', 'Color', [1 0.4 0]);
    legend([h_data(1), h_fit, h_straight, h_ci], ...
        {'data', sprintf('fit (PSE = %.3f)', pse), ...
         task.reference_label, ...
         sprintf('95%% credible interval [%.3f, %.3f]', pse_ci95(1), pse_ci95(2))}, ...
        'Location', 'best');

    safe_id = regexprep(char(participant_id), '[^A-Za-z0-9_-]', '_');
    plot_file = fullfile(data_folder, ...
        sprintf('%s_psychometric_psignifit_%s%s.png', task.output_prefix, safe_id, condition_suffix));
    saveas(gcf, plot_file);
    fprintf('plot saved: %s\n', plot_file);
end

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

function values = conditionValues(T, name)
    values = string(T.(name));
    % Free head movement can leave a target speed unspecified in every row.
    % Treat repeated missing entries as one condition, not different values.
    values(ismissing(values)) = "<unspecified>";
end

function value = resultValue(T, name, task)
    if ismember(name, task.pooled_columns)
        % Pooled columns are always text ("60 (100); 70 (75)"), so the summary
        % of several conditions can still be stacked into one table.
        value = valueCounts(T, name);
    elseif isnumeric(T.(name))
        value = T.(name)(1);
    else
        value = string(T.(name)(1));
    end
end

function text = plotValue(T, name, task)
    if ismember(name, task.pooled_columns)
        text = "mixed";
    else
        text = string(T.(name)(1));
    end
end

function text = valueCounts(T, name)
    % "0.5 (100); 1 (75)": every value with its number of valid trials.
    values = conditionValues(T, name);
    [unique_values, ~, index] = unique(values, 'stable');
    counts = accumarray(index, 1);
    text = join(unique_values + " (" + string(counts) + ")", "; ");
end

function [group_keys, group_id] = conditionGroups(T, split_columns)
    present = split_columns(ismember(split_columns, T.Properties.VariableNames));
    parts = strings(height(T), 0);
    for index = 1:numel(present)
        values = conditionValues(T, present{index});
        % Only columns that actually vary appear in the label.
        if numel(unique(values)) > 1
            parts(:, end + 1) = string(present{index}) + "=" + values; %#ok<AGROW>
        end
    end
    if isempty(parts)
        group_keys = "single condition";
        group_id = ones(height(T), 1);
        return;
    end
    [group_keys, ~, group_id] = unique(join(parts, ", ", 2), 'stable');
end

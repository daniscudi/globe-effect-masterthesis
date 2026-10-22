function test_instrument_schema
% Deterministic synthetic schema checks, NOT participant or pilot observations.
analysis_folder = fileparts(fileparts(mfilename('fullpath')));
script_path = fullfile(analysis_folder, 'fit_checkerboard_pse_psignifit.m');
test_root = tempname;
assert(startsWith(test_root, tempdir, 'IgnoreCase', true), 'Test output must remain in the temp directory.');
mkdir(test_root);
cleanup = onCleanup(@() rmdir(test_root, 's')); %#ok<NASGU>
old_visibility = get(groot, 'defaultFigureVisible');
visibility_cleanup = onCleanup(@() set(groot, 'defaultFigureVisible', old_visibility)); %#ok<NASGU>
set(groot, 'defaultFigureVisible', 'off');

levels = [0.2, 0.4, 0.6, 0.8, 1.0, 1.2, 1.4];
convex_counts = [0, 1, 4, 12, 22, 24, 25];
k = repelem(levels(:), 25);
n = numel(k);
response = repmat("Concave", n, 1);
for i = 1:numel(levels)
    response((i-1)*25 + (1:convex_counts(i))) = "Convex";
end
T = table(repmat("schema_test", n, 1), repmat("2026-10-18T12:00:00", n, 1), ...
    repmat("BothEyes", n, 1), repmat(60, n, 1), (1:n)', response, ones(n, 1), ...
    k, repmat(10, n, 1), repmat(8, n, 1), repmat("Checkerboard", n, 1), ...
    repmat("merlitz-instrument-checkerboard-fixed-source-grid-v1", n, 1), ...
    'VariableNames', {'participant_id', 'session_start_utc', 'eye_presentation', ...
    'angular_diameter_deg', 'sequence_index', 'response', 'valid_for_analysis', ...
    'instrument_distortion_k', 'instrument_magnification_m', 'grid_line_spacing_deg', ...
    'stimulus_task', 'mapping_version'});

csv = fullfile(test_root, 'instrument_trials.csv');
writetable(T, csv);
runFit(script_path, csv);
result_path = fullfile(test_root, 'checkerboard_instrument_pse_result_psignifit_m10.xlsx');
assert(isfile(result_path), 'New checkerboard was not classified as checkerboard.');
result = readtable(result_path);
assert(ismember('pse_instrument_distortion_k', result.Properties.VariableNames));
assert(strcmpi(string(result.task(1)), "Checkerboard"));

% One randomized session contains both grid variants. They must yield two
% independent fits/files, never one pooled PSE or overwritten result.
fixed_grid = T;
fixed_grid.grid_mapping = repmat("FixedSourceGrid", n, 1);
edge_grid = T;
edge_grid.sequence_index = edge_grid.sequence_index + n;
edge_grid.grid_mapping = repmat("EdgeNormalized", n, 1);
edge_grid.mapping_version(:) = "merlitz-instrument-checkerboard-edge-normalized-v1";
edge_counts = [0, 0, 1, 4, 12, 22, 25];
edge_grid.response(:) = "Concave";
for i = 1:numel(levels)
    edge_grid.response((i-1)*25 + (1:edge_counts(i))) = "Convex";
end
both = [fixed_grid; edge_grid];
% Deterministic interleaving, so sequence order does not stand for variant.
both = both(reshape([1:n; n+(1:n)], [], 1), :);
csv = fullfile(test_root, 'both_grid_variants_trials.csv');
writetable(both, csv);
runFit(script_path, csv);
fixed_result = readtable(fullfile(test_root, ...
    'checkerboard_instrument_pse_result_psignifit_m10_gridFixedSourceGrid.xlsx'));
edge_result = readtable(fullfile(test_root, ...
    'checkerboard_instrument_pse_result_psignifit_m10_gridEdgeNormalized.xlsx'));
assert(fixed_result.n_trials == n && edge_result.n_trials == n);
assert(abs(fixed_result.pse_instrument_distortion_k - result.pse_instrument_distortion_k) < 1e-5);
assert(edge_result.pse_instrument_distortion_k > fixed_result.pse_instrument_distortion_k + 0.05);
summary = readtable(fullfile(test_root, 'checkerboard_instrument_pse_summary_psignifit.xlsx'));
assert(height(summary) == 2 && numel(unique(string(summary.grid_mapping))) == 2);

bad_grid = both;
bad_grid.grid_mapping(:) = "FixedSourceGrid";
writetable(bad_grid, csv);
rejected_grid = false;
try
    runFit(script_path, csv);
catch error_info
    rejected_grid = contains(error_info.message, 'metadata', 'IgnoreCase', true);
end
assert(rejected_grid, 'Grid variant and mapping version must agree.');

% The custom tan/tanh extension exists only in the Unity live preview. Files with
% its mapping version are rejected instead of being analysed as Merlitz k.
extended = T;
extended.mapping_version(:) = "custom-tan-tanh-checkerboard-fixed-source-grid-v1";
csv = fullfile(test_root, 'extended_trials.csv');
writetable(extended, csv);
rejected = false;
try
    runFit(script_path, csv);
catch error_info
    rejected = contains(error_info.message, 'mapping', 'IgnoreCase', true);
end
assert(rejected, 'Custom tan/tanh checkerboard files must be rejected.');
rejected = false;

old = removevars(T, {'stimulus_task', 'instrument_magnification_m'});
old = renamevars(old, 'instrument_distortion_k', 'visual_space_l');
old.mapping_version(:) = "visual-space-l-tangent-normalized-cartesian-grid-aperture-halfmax-v3";
csv = fullfile(test_root, 'legacy_trials.csv');
writetable(old, csv);
runFit(script_path, csv);
assert(isfile(fullfile(test_root, 'checkerboard_pse_result_psignifit.xlsx')));
old_result = readtable(fullfile(test_root, 'checkerboard_pse_result_psignifit.xlsx'));
assert(ismember('pse_visual_space_l', old_result.Properties.VariableNames));

dots = removevars(T, {'stimulus_task', 'grid_line_spacing_deg'});
dots.motion_mode = repmat("SimulatedYaw", n, 1);
dots.content_zoom = ones(n, 1);
dots.mapping_version(:) = "synthetic-random-dot-instrument-test";
csv = fullfile(test_root, 'dots_trials.csv');
writetable(dots, csv);
runFit(script_path, csv);
assert(~isempty(dir(fullfile(test_root, 'random_dot_pse_result_psignifit*.xlsx'))));

mixed = T;
mixed.mapping_version(end) = "unexpected-legacy-mapping";
csv = fullfile(test_root, 'mixed_mapping_trials.csv');
writetable(mixed, csv);
rejected = false;
try
    runFit(script_path, csv);
catch error_info
    rejected = contains(error_info.message, 'mapping', 'IgnoreCase', true);
end
assert(rejected, 'Mixed checkerboard mappings must be rejected.');
fprintf('\nPASS: instrument, separate grid-variant PSEs, custom tan/tanh rejected, historical l, random dots, metadata/mapping rejection.\n');
end

function runFit(script_path, csv_path)
% The analysis script clears local variables. Keep fixtures in the caller.
run(script_path);
end

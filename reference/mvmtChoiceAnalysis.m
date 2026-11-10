% Clears workspace and closes figures
clear;
close all;

% Get the directory of the currently running script
scriptDir = fileparts(mfilename('fullpath'));

% Set the working directory to the script's directory
cd(scriptDir);

% Display the new working directory
disp(['Current working directory: ', pwd]);


% Define file paths using relative paths

% Set these to one session's pair of files (same participant code and date-time).
% (Example paths from the lab's own machines were removed for the course copy.)
isSimple = false;   % true for the Simple / SimpleOneBall protocols
rtData = readtable("path\to\<ID><Scene>rtData-<date-time>.csv");
eyeData = readtable("path\to\<ID><Scene>eyeTr-<date-time>.csv", 'Delimiter', ';');
histTitle = "<ID> <Scene>; ";

% Toggles
apply_smoothing = true;  % Set to true to apply moving average smoothing, false otherwise
use_line_plot = true;    % Set to true to connect points with lines, false for scatterplots

% Define parameters
msBeforeChange = 400;    % Time window before reaction time
msAfterChange = 1000;    % Time window after reaction time
timeDelay = 0;           % Delay between ball split and eye movement (adjust if needed)
yOffset = 1;
xOffset = 0;
eccYLim = 12;            % the width/height of the window in degrees ecc
figureSize = [50, 50, 600, 600]; % Adjust size as needed
ballSpeed = 10;

% Calculate reaction times
reaction_times = rtData.SplitTime - rtData.CueShowTime;

ballDirection = rtData.direction;
redDirection = rtData.redDirection;
ballStopX = rtData.xEcc;

% Extract eye-tracking data for X and Y eccentricity
x_eccentricity = eyeData.CalcXEccentricity;
y_eccentricity = eyeData.CalcYEccentricity;

% Apply smoothing if toggle is enabled
if apply_smoothing
    x_eccentricity = movmean(x_eccentricity, 3); % Moving average of 3 points
    y_eccentricity = movmean(y_eccentricity, 3); % Moving average of 3 points
end

y_eccentricity = y_eccentricity - yOffset;
x_eccentricity = x_eccentricity - xOffset;

% Create a 'Figures' folder if it doesn't already exist
figures_folder = './Figures';
if ~exist(figures_folder, 'dir')
    mkdir(figures_folder);
end

% Define global axis limits for consistency
time_xlim = [-msBeforeChange, msAfterChange]; % X-axis limits for time plots

% Initialize arrays to store peak data
num_trials = length(reaction_times);
peak_times = NaN(num_trials, 1);
peak_values = NaN(num_trials, 1);
avg_y_all = NaN(num_trials, 1);  % <-- Store avg_y for each trial

for i = 10:20%1:length(reaction_times)
    % Determine reaction time and time window
    reaction_time = rtData.CueShowTime(i) + reaction_times(i) + timeDelay;
    time_start = reaction_time - msBeforeChange;
    time_end = reaction_time + msAfterChange;

    % Filter eye-tracking data within the time window
    capture_time = eyeData.CaptureTime;
    window_indices = (capture_time >= time_start) & (capture_time <= time_end);
    filtered_times = capture_time(window_indices);
    filtered_x_eccentricity = x_eccentricity(window_indices);
    filtered_y_eccentricity = y_eccentricity(window_indices);
    
    % Shift time axis to make reaction time point 0
    shifted_times = filtered_times - reaction_time;
    
    % Calculate Y and X axis limits for eccentricities
    y_center = (max(filtered_y_eccentricity) + min(filtered_y_eccentricity)) / 2;
    x_center = (max(filtered_x_eccentricity) + min(filtered_x_eccentricity)) / 2;
    
    y_range = max(filtered_y_eccentricity) - min(filtered_y_eccentricity);
    x_range = max(filtered_x_eccentricity) - min(filtered_x_eccentricity);
    
    y_lim = max(eccYLim/2, y_range / 2);
    x_lim = max(eccYLim/2, x_range / 2);
    
    y_ecc_ylim = [y_center - y_lim, y_center + y_lim];
    x_ecc_ylim = [x_center - x_lim, x_center + x_lim];
    
        % Determine reaction time and time window
    reaction_time = rtData.CueShowTime(i) + reaction_times(i) + timeDelay;
    time_start = reaction_time - msBeforeChange;
    time_end = reaction_time + msAfterChange;
    
    % Filter eye-tracking data within the time window
    capture_time = eyeData.CaptureTime;
    window_indices = (capture_time >= time_start) & (capture_time <= time_end);
    filtered_times = capture_time(window_indices);
    filtered_y_eccentricity = y_eccentricity(window_indices);

    % Compute velocity
    delta_time = diff(filtered_times) / 1000; % Convert to seconds
    y_velocity = diff(filtered_y_eccentricity) ./ delta_time; % Y velocity (°/s)
    velocity_times = filtered_times(1:end-1) - reaction_time; % Align with reaction time

    % Compute absolute value of Y velocity
    abs_y_velocity = abs(y_velocity);

    % Find peaks after t = 20
    valid_indices = velocity_times > 100; % Only consider peaks 20 ms after reaction time
    [pks, locs] = findpeaks(abs_y_velocity(valid_indices), velocity_times(valid_indices), 'MinPeakProminence', 100);

    if ~isempty(pks)
        % Store the first peak and corresponding time
        peak_values(i) = pks(1);
        peak_times(i) = locs(1);
    end
    
    % Compute the average of filtered_y_eccentricity
    avg_y = mean(filtered_y_eccentricity);
    avg_y_all(i) = avg_y;

    
    %% Figure 1: Y Eccentricity vs Time
    fig1 = figure('Position', figureSize);
    hold on;
    
    % Plot data with a dotted line or points
    if use_line_plot
        plot(shifted_times, filtered_y_eccentricity, 'b-', 'LineWidth', 1.5); % Dotted blue line
    else
        plot(shifted_times, filtered_y_eccentricity, 'b.', 'MarkerSize', 10); % Blue points
    end
    
    % Add vertical line at T=0
    xline(0, 'r--', 'LineWidth', 1.5, 'DisplayName', 'T = 0'); % Dotted red line
    
    line([-msBeforeChange, 0], [0 0], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2, 'DisplayName', 'Negative Avg Line');

    if isSimple
        % Draw dotted black line from (0,0) to (1000,10)
        line([0 msAfterChange], [0 (ballSpeed*msAfterChange/1000)], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2, 'DisplayName', 'Positive Avg Line');
        % Draw dotted black line from (0,0) to (1000,-10)
        line([0 msAfterChange], [0 -(ballSpeed*msAfterChange/1000)], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2, 'DisplayName', 'Negative Avg Line');
    else
        % Draw line segment based on whether avg_y is above or below zero
        if redDirection(i) < 0
            % Draw dotted black line from (0,0) to (1000,10)
            line([0 msAfterChange], [0 (ballSpeed*msAfterChange/1000)], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2, 'DisplayName', 'Positive Avg Line');
        else
            % Draw dotted black line from (0,0) to (1000,-10)
            line([0 msAfterChange], [0 -(ballSpeed*msAfterChange/1000)], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2, 'DisplayName', 'Negative Avg Line');
        end
    end
    % If a peak was found, plot vertical line at peak time
    if ~isnan(peak_times(i))
        xline(peak_times(i), 'b--', 'LineWidth', 1.5, 'DisplayName', 'First Peak');
    end
    % Add labels and title
    title(sprintf('Trial %d: Y-Eccentricity vs Time', i));
    xlabel('Time Relative to Reaction (ms)');
    ylabel('Y Eccentricity (°)');
    
    % Set axis limits
    xlim(time_xlim);
    ylim(y_ecc_ylim);
    
    % Add legend
    legend('Data', 'T = 0', 'Ball Path', 'Location', 'best');
    
    % Add grid and release hold
    grid on;
    hold off;

    saveas(fig1, fullfile(figures_folder, sprintf('Trial_%d_YEcc.png', i)));
    close(fig1);

    
    %% Figure 2: X vs. Y Eccentricity with Time as Color
    %  fig2 = figure('Position', figureSize);
    % hold on;
    % if use_line_plot
    %     normalized_times = (shifted_times - min(shifted_times)) / (max(shifted_times) - min(shifted_times));
    %     cmap = colormap(winter);
    %     num_colors = size(cmap, 1);
    % 
    %     for j = 1:length(filtered_x_eccentricity) - 1
    %         x_segment = [filtered_x_eccentricity(j), filtered_x_eccentricity(j+1)];
    %         y_segment = [filtered_y_eccentricity(j), filtered_y_eccentricity(j+1)];
    % 
    %         color_index = max(1, round(normalized_times(j) * (num_colors - 1)) + 1);
    %         segment_color = cmap(color_index, :);
    % 
    %         line(x_segment, y_segment, 'Color', segment_color, 'LineWidth', 1.5);
    %     end
    % else
    %     scatter(filtered_x_eccentricity, filtered_y_eccentricity, 20, shifted_times, 'filled');
    % end
    % 
    % line([ballStopX(i), 0], [0 0], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2);
    % if isSimple
    %     line([ballStopX(i), ballStopX(i)], [-10,10], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2);
    % else
    %     line([ballStopX(i), ballStopX(i)], [0, 10 * -1 * redDirection(i)], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2);
    % end
    % 
    % cb = colorbar; % Add colorbar
    % colormap(winter); 
    % cb.Label.String = 'Time Relative to Direction Change (ms)'; 
    % title(sprintf('Trial %d: X vs. Y Eccentricity over Time', i));
    % xlabel('X Eccentricity (°)');
    % ylabel('Y Eccentricity (°)');
    % xlim(x_ecc_ylim);
    % ylim(y_ecc_ylim);
    % grid on;
    % hold off;
    % saveas(fig2, fullfile(figures_folder, sprintf('Trial_%d_XYTime.png', i)));
    % close(fig2);
    % %

    % %% Figure 3: X Eccentricity vs Time
    % fig3 = figure('Position', figureSize);
    % hold on;
    % if use_line_plot
    %     plot(shifted_times, filtered_x_eccentricity, 'm-', 'LineWidth', 1.5);
    % else
    %     plot(shifted_times, filtered_x_eccentricity, 'm.');
    % end
    % 
    % line([-msBeforeChange, 0], [ballStopX(i) - (ballDirection(i) * ballSpeed*msBeforeChange/1000), ballStopX(i)], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2);
    % line([0, msAfterChange], [ballStopX(i), ballStopX(i)], 'Color', 'k', 'LineStyle', '--', 'LineWidth', 2);
    % xline(0, 'r--', 'LineWidth', 1.5);
    % title(sprintf('Trial %d: X Eccentricity vs Time', i));
    % xlabel('Time Relative to Reaction (ms)');
    % ylabel('X Eccentricity (°)');
    % xlim(time_xlim);
    % ylim(x_ecc_ylim);
    % grid on;
    % hold off;
    % saveas(fig3, fullfile(figures_folder, sprintf('Trial_%d_XEcc.png', i)));
    % close(fig3);
    % 
    % % Calculate velocities
    % delta_time = diff(filtered_times) / 1000; % Convert time differences to seconds
    % x_velocity = diff(filtered_x_eccentricity) ./ delta_time; % X velocity (°/s)
    % y_velocity = diff(filtered_y_eccentricity) ./ delta_time; % Y velocity (°/s)
    % velocity_times = shifted_times(1:end-1); % Adjust times for velocity plots
    % 
    % %
    % %% Figure 4: X Velocity vs Time
    % fig4 = figure('Position', figureSize);
    % hold on;
    % plot(velocity_times, x_velocity, 'c-', 'LineWidth', 1.5); % Cyan line
    % xline(0, 'r--', 'LineWidth', 1.5); % Reaction time marker
    % % If a peak was found, plot vertical line at peak time
    % if ~isnan(peak_times(i))
    %     xline(peak_times(i), 'b--', 'LineWidth', 1.5, 'DisplayName', 'First Peak');
    % end
    % title(sprintf('Trial %d: X Velocity vs Time', i));
    % xlabel('Time Relative to Reaction (ms)');
    % ylabel('X Velocity (°/s)');
    % xlim(time_xlim);
    % grid on;
    % hold off;
    % saveas(fig4, fullfile(figures_folder, sprintf('Trial_%d_XVelocity.png', i)));
    % close(fig4);
    % %
    %% Figure 5: Y Velocity vs Time with Peak Marker
    fig5 = figure('Position', figureSize);
    hold on;
    plot(velocity_times, y_velocity, 'g-', 'LineWidth', 1.5); % Green line for velocity
    xline(0, 'r--', 'LineWidth', 1.5, 'DisplayName', 'T = 0'); % Reaction time marker
    
    % If a peak was found, plot vertical line at peak time
    if ~isnan(peak_times(i))
        xline(peak_times(i), 'b--', 'LineWidth', 1.5, 'DisplayName', 'First Peak');
    end
    
    % Titles and Labels
    title(sprintf('Trial %d: Y Velocity vs Time', i));
    xlabel('Time Relative to Reaction (ms)');
    ylabel('Y Velocity (°/s)');
    
    % Set X limits
    xlim(time_xlim);
    grid on;
    hold off;
    
    % Save figure
    saveas(fig5, fullfile(figures_folder, sprintf('Trial_%d_YVelocity.png', i)));
    close(fig5);


    %}
end
%% Figure 6: histogram of peak times (after removing outliers)
% Remove NaN values
valid_peak_times = peak_times(~isnan(peak_times));

% Compute IQR-based outlier thresholds
Q1 = quantile(valid_peak_times, 0.25);
Q3 = quantile(valid_peak_times, 0.75);
IQR_value = Q3 - Q1;
lower_bound = Q1 - 1.5 * IQR_value;
upper_bound = Q3 + 1.5 * IQR_value;

% Remove outliers
filtered_peak_times = valid_peak_times(valid_peak_times >= lower_bound & valid_peak_times <= upper_bound);

% Compute statistics after outlier removal
mean_peak = mean(filtered_peak_times);
std_peak = std(filtered_peak_times);
n = length(filtered_peak_times);
sem_peak = std_peak / sqrt(n); % Standard Error of the Mean (SEM)
ci_95 = 1.96 * sem_peak; % 95% Confidence Interval

% Create histogram
fig_hist = figure('Position', figureSize);
histogram(filtered_peak_times, 'BinWidth', 25, 'FaceColor', 'b', 'EdgeColor', 'k'); % Adjust bin width as needed

% Set X limits
xlim([0, 600]);
ylim([0, 25]);

% Add mean and confidence interval lines
hold on;
y_limits = ylim; % Get Y-axis limits
plot([mean_peak, mean_peak], y_limits, 'r--', 'LineWidth', 2, 'DisplayName', 'Mean');
plot([mean_peak - ci_95, mean_peak - ci_95], y_limits, 'g--', 'LineWidth', 1.5, 'DisplayName', '95% CI');
plot([mean_peak + ci_95, mean_peak + ci_95], y_limits, 'g--', 'LineWidth', 1.5, 'DisplayName', '95% CI');

% Add labels and title
xlabel('Time to First Peak (ms)');
ylabel('Frequency');
title(histTitle + 'Histogram of First Large Peak Times in |Y Velocity|');

% Add statistics text (Mean, Standard Deviation, Standard Error)
text(425, max(y_limits) * 0.9, sprintf('Mean: %.2f ms\nSD: %.2f ms\nSEM: %.2f ms', mean_peak, std_peak, sem_peak), 'FontSize', 12, 'Color', 'k');

% Save figure
saveas(fig_hist, fullfile(figures_folder, 'YVelPeakHist.png'));
hold off;
%% New Section: Histograms by avg_y Sign (After Outlier Removal)

% Filter out trials with NaN values in peak_times or avg_y_all
valid_idx = ~isnan(peak_times) & ~isnan(avg_y_all);
peak_times_valid = peak_times(valid_idx);
avg_y_valid = avg_y_all(valid_idx);

% Separate trials based on the sign of avg_y
neg_peak_times = peak_times_valid(avg_y_valid < 0);
pos_peak_times = peak_times_valid(avg_y_valid >= 0);

% Create a figure with two subplots
% Double the width (third element)
figureSize(3) = figureSize(3) * 2.25;

% Create the figure using the updated figureSize
fig_hist_groups = figure('Position', figureSize);

%% Left Subplot: Negative avg_y Trials
subplot(1,2,1);
% Remove outliers using IQR for the negative group
Q1_neg = quantile(neg_peak_times, 0.25);
Q3_neg = quantile(neg_peak_times, 0.75);
IQR_neg = Q3_neg - Q1_neg;
lower_bound_neg = Q1_neg - 1.5 * IQR_neg;
upper_bound_neg = Q3_neg + 1.5 * IQR_neg;
filtered_neg_peak_times = neg_peak_times(neg_peak_times >= lower_bound_neg & neg_peak_times <= upper_bound_neg);

% Compute statistics
mean_neg = mean(filtered_neg_peak_times);
std_neg = std(filtered_neg_peak_times);
n_neg = length(filtered_neg_peak_times);
sem_neg = std_neg / sqrt(n_neg);   % Standard Error of the Mean
ci_neg = 1.96 * sem_neg;             % 95% Confidence Interval

% Create histogram
histogram(filtered_neg_peak_times, 'BinWidth', 25, 'FaceColor', 'r', 'EdgeColor', 'k');
xlim([0, 600]);
ylim([0, 25]);  % Adjust as needed

% Add mean and confidence interval lines
hold on;
y_limits = ylim;
plot([mean_neg, mean_neg], y_limits, 'r--', 'LineWidth', 2, 'DisplayName', 'Mean');
plot([mean_neg - ci_neg, mean_neg - ci_neg], y_limits, 'g--', 'LineWidth', 1.5, 'DisplayName', '95% CI');
plot([mean_neg + ci_neg, mean_neg + ci_neg], y_limits, 'g--', 'LineWidth', 1.5, 'DisplayName', '95% CI');

% Add labels and title
xlabel('Time to First Peak (ms)');
ylabel('Frequency');
title('Down: Histogram of Peak Times');
text(425, max(y_limits) * 0.9, sprintf('Mean: %.2f ms\nSD: %.2f ms\nSEM: %.2f ms', mean_neg, std_neg, sem_neg), ...
    'FontSize', 12, 'Color', 'k');
hold off;

%% Right Subplot: Positive avg_y Trials
subplot(1,2,2);
% Remove outliers using IQR for the positive group
Q1_pos = quantile(pos_peak_times, 0.25);
Q3_pos = quantile(pos_peak_times, 0.75);
IQR_pos = Q3_pos - Q1_pos;
lower_bound_pos = Q1_pos - 1.5 * IQR_pos;
upper_bound_pos = Q3_pos + 1.5 * IQR_pos;
filtered_pos_peak_times = pos_peak_times(pos_peak_times >= lower_bound_pos & pos_peak_times <= upper_bound_pos);

% Compute statistics
mean_pos = mean(filtered_pos_peak_times);
std_pos = std(filtered_pos_peak_times);
n_pos = length(filtered_pos_peak_times);
sem_pos = std_pos / sqrt(n_pos);   % Standard Error of the Mean
ci_pos = 1.96 * sem_pos;             % 95% Confidence Interval

% Create histogram
histogram(filtered_pos_peak_times, 'BinWidth', 25, 'FaceColor', 'b', 'EdgeColor', 'k');
xlim([0, 600]);
ylim([0, 25]);  % Adjust as needed

% Add mean and confidence interval lines
hold on;
y_limits = ylim;
plot([mean_pos, mean_pos], y_limits, 'r--', 'LineWidth', 2, 'DisplayName', 'Mean');
plot([mean_pos - ci_pos, mean_pos - ci_pos], y_limits, 'g--', 'LineWidth', 1.5, 'DisplayName', '95% CI');
plot([mean_pos + ci_pos, mean_pos + ci_pos], y_limits, 'g--', 'LineWidth', 1.5, 'DisplayName', '95% CI');

% Add labels and title
xlabel('Time to First Peak (ms)');
ylabel('Frequency');
title('Up: Histogram of Peak Times');
text(425, max(y_limits) * 0.9, sprintf('Mean: %.2f ms\nSD: %.2f ms\nSEM: %.2f ms', mean_pos, std_pos, sem_pos), ...
    'FontSize', 12, 'Color', 'k');
hold off;

% Save the figure
saveas(fig_hist_groups, fullfile(figures_folder, 'YVelPeakHist_by_avgY.png'));

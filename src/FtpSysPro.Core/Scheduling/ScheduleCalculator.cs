using System;
using System.Collections.Generic;
using System.Linq;
using FtpSysPro.Core.Models;

namespace FtpSysPro.Core.Scheduling
{
    public static class ScheduleCalculator
    {
        public static DateTime? GetNextRun(ScheduleDefinition schedule, DateTime fromLocalTime)
        {
            if (schedule == null || !schedule.Enabled)
                return null;

            switch (schedule.IntervalType)
            {
                case ScheduleIntervalType.Once:
                    if (schedule.RunOnceLocalTime.HasValue && schedule.RunOnceLocalTime.Value > fromLocalTime)
                        return schedule.RunOnceLocalTime.Value;
                    return null;

                case ScheduleIntervalType.Daily:
                    return GetNextDaily(schedule, fromLocalTime);

                case ScheduleIntervalType.Weekly:
                    return GetNextWeekly(schedule, fromLocalTime);

                case ScheduleIntervalType.Monthly:
                    return GetNextMonthly(schedule, fromLocalTime);

                case ScheduleIntervalType.RepeatEvery:
                    return GetNextRepeatEvery(schedule, fromLocalTime);

                default:
                    return null;
            }
        }

        public static DateTime? GetNextJobRun(BackupJob job, DateTime fromLocalTime)
        {
            if (job == null || !job.Enabled || job.Schedules == null || job.Schedules.Count == 0)
                return null;

            DateTime? earliest = null;
            foreach (var s in job.Schedules)
            {
                var next = GetNextRun(s, fromLocalTime);
                if (next.HasValue)
                {
                    if (!earliest.HasValue || next.Value < earliest.Value)
                    {
                        earliest = next.Value;
                    }
                }
            }
            return earliest;
        }

        private static DateTime? GetNextDaily(ScheduleDefinition schedule, DateTime fromLocalTime)
        {
            var tod = ParseTimeOfDay(schedule.TimeOfDay);
            var todayTarget = fromLocalTime.Date.Add(tod);

            if (todayTarget > fromLocalTime)
                return todayTarget;

            return todayTarget.AddDays(1);
        }

        private static DateTime? GetNextWeekly(ScheduleDefinition schedule, DateTime fromLocalTime)
        {
            if (schedule.DaysOfWeek == null || schedule.DaysOfWeek.Count == 0)
                return null;

            var tod = ParseTimeOfDay(schedule.TimeOfDay);
            var sortedDays = schedule.DaysOfWeek.Distinct().OrderBy(d => d).ToList();

            // Check upcoming 14 days
            for (int i = 0; i < 14; i++)
            {
                var checkDate = fromLocalTime.Date.AddDays(i);
                int dayOfWeekInt = (int)checkDate.DayOfWeek;

                if (sortedDays.Contains(dayOfWeekInt))
                {
                    var targetTime = checkDate.Add(tod);
                    if (targetTime > fromLocalTime)
                        return targetTime;
                }
            }
            return null;
        }

        private static DateTime? GetNextMonthly(ScheduleDefinition schedule, DateTime fromLocalTime)
        {
            var tod = ParseTimeOfDay(schedule.TimeOfDay);
            int targetDay = Math.Max(1, Math.Min(31, schedule.DayOfMonth));

            // Check current month and next 2 months
            for (int monthOffset = 0; monthOffset < 3; monthOffset++)
            {
                var refMonth = fromLocalTime.Date.AddMonths(monthOffset);
                int daysInMonth = DateTime.DaysInMonth(refMonth.Year, refMonth.Month);
                int clampedDay = Math.Min(targetDay, daysInMonth);

                var targetTime = new DateTime(refMonth.Year, refMonth.Month, clampedDay).Add(tod);
                if (targetTime > fromLocalTime)
                    return targetTime;
            }
            return null;
        }

        private static DateTime? GetNextRepeatEvery(ScheduleDefinition schedule, DateTime fromLocalTime)
        {
            int intervalMinutes = Math.Max(1, schedule.RepeatIntervalMinutes);
            var startTime = ParseTimeOfDay(schedule.RepeatStartTime);
            var endTime = ParseTimeOfDay(schedule.RepeatEndTime);

            if (endTime <= startTime)
            {
                endTime = TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59));
            }

            // Check today within start/end
            var today = fromLocalTime.Date;
            var windowStart = today.Add(startTime);
            var windowEnd = today.Add(endTime);

            if (fromLocalTime < windowStart)
            {
                return windowStart;
            }

            if (fromLocalTime <= windowEnd)
            {
                // Advance by intervals
                var elapsedMinutes = (fromLocalTime - windowStart).TotalMinutes;
                var intervalsPassed = (int)Math.Floor(elapsedMinutes / intervalMinutes);
                var nextCandidate = windowStart.AddMinutes((intervalsPassed + 1) * intervalMinutes);

                if (nextCandidate <= windowEnd)
                    return nextCandidate;
            }

            // Move to tomorrow's window start
            return today.AddDays(1).Add(startTime);
        }

        private static TimeSpan ParseTimeOfDay(string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr))
                return TimeSpan.FromHours(2); // default 02:00

            if (TimeSpan.TryParse(timeStr, out var ts))
                return ts;

            var parts = timeStr.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m))
            {
                return new TimeSpan(Math.Max(0, Math.Min(23, h)), Math.Max(0, Math.Min(59, m)), 0);
            }

            return TimeSpan.FromHours(2);
        }
    }
}

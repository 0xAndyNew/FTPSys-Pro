using System;
using System.Collections.Generic;
using FtpSysPro.Core.Models;
using FtpSysPro.Core.Scheduling;
using Xunit;

namespace FtpSysPro.Core.Tests
{
    public class SchedulingTests
    {
        [Fact]
        public void DailySchedule_CalculatesSameDay_WhenBeforeTime()
        {
            var schedule = new ScheduleDefinition
            {
                Enabled = true,
                IntervalType = ScheduleIntervalType.Daily,
                TimeOfDay = "14:00"
            };

            var from = new DateTime(2026, 5, 10, 10, 0, 0);
            var next = ScheduleCalculator.GetNextRun(schedule, from);

            Assert.NotNull(next);
            Assert.Equal(new DateTime(2026, 5, 10, 14, 0, 0), next.Value);
        }

        [Fact]
        public void DailySchedule_CalculatesNextDay_WhenAfterTime()
        {
            var schedule = new ScheduleDefinition
            {
                Enabled = true,
                IntervalType = ScheduleIntervalType.Daily,
                TimeOfDay = "14:00"
            };

            var from = new DateTime(2026, 5, 10, 15, 0, 0);
            var next = ScheduleCalculator.GetNextRun(schedule, from);

            Assert.NotNull(next);
            Assert.Equal(new DateTime(2026, 5, 11, 14, 0, 0), next.Value);
        }

        [Fact]
        public void WeeklySchedule_FindsUpcomingDay()
        {
            // 2026-05-10 is a Sunday (DayOfWeek = 0)
            var schedule = new ScheduleDefinition
            {
                Enabled = true,
                IntervalType = ScheduleIntervalType.Weekly,
                TimeOfDay = "09:00",
                DaysOfWeek = new List<int> { 1, 3, 5 } // Mon, Wed, Fri
            };

            var from = new DateTime(2026, 5, 10, 10, 0, 0); // Sunday 10am
            var next = ScheduleCalculator.GetNextRun(schedule, from);

            Assert.NotNull(next);
            // Next should be Monday May 11 at 09:00
            Assert.Equal(new DateTime(2026, 5, 11, 9, 0, 0), next.Value);
        }

        [Fact]
        public void MonthlySchedule_ClampsToDaysInMonth()
        {
            var schedule = new ScheduleDefinition
            {
                Enabled = true,
                IntervalType = ScheduleIntervalType.Monthly,
                TimeOfDay = "03:00",
                DayOfMonth = 31
            };

            // From Feb 1, 2026 (non-leap year, 28 days)
            var from = new DateTime(2026, 2, 1, 0, 0, 0);
            var next = ScheduleCalculator.GetNextRun(schedule, from);

            Assert.NotNull(next);
            // Clamped to Feb 28
            Assert.Equal(new DateTime(2026, 2, 28, 3, 0, 0), next.Value);
        }

        [Fact]
        public void RepeatEverySchedule_AdvancesByIntervalWithinWindow()
        {
            var schedule = new ScheduleDefinition
            {
                Enabled = true,
                IntervalType = ScheduleIntervalType.RepeatEvery,
                RepeatIntervalMinutes = 30,
                RepeatStartTime = "08:00",
                RepeatEndTime = "18:00"
            };

            var from = new DateTime(2026, 5, 10, 8, 10, 0);
            var next = ScheduleCalculator.GetNextRun(schedule, from);

            Assert.NotNull(next);
            Assert.Equal(new DateTime(2026, 5, 10, 8, 30, 0), next.Value);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    public class OsuDifficultyCalculator : DifficultyCalculator
    {
        private const double difficulty_multiplier = 0.0675;

        public OsuDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            if (beatmap.HitObjects.Count == 0)
                return new OsuDifficultyAttributes { Mods = mods, Skills = skills };

            double aimRating = Math.Sqrt(skills[0].DifficultyValue()) * difficulty_multiplier;
            double jumpAimRating = Math.Sqrt(skills[2].DifficultyValue()) * difficulty_multiplier;
            double flowAimRating = Math.Sqrt(skills[3].DifficultyValue()) * difficulty_multiplier;
            double precisionRating = Math.Sqrt(Math.Max(0, skills[0].DifficultyValue() - skills[1].DifficultyValue())) * difficulty_multiplier;

            double speedRating = Math.Sqrt(skills[4].DifficultyValue()) * difficulty_multiplier;
            double staminaRating = Math.Sqrt(skills[5].DifficultyValue()) * difficulty_multiplier;

            double accuracyRating = skills[6].DifficultyValue();

            double starRating = Math.Pow(Math.Pow(aimRating, 3) + Math.Pow(Math.Max(speedRating, staminaRating), 3), 1 / 3.0) * 1.6;

            HitWindows hitWindows = new OsuHitWindows();
            hitWindows.SetDifficulty(beatmap.BeatmapInfo.BaseDifficulty.OverallDifficulty);

            // Todo: These int casts are temporary to achieve 1:1 results with osu!stable, and should be removed in the future
            double hitWindowGreat = (int)(hitWindows.WindowFor(HitResult.Great)) / clockRate;
            double preempt = (int)BeatmapDifficulty.DifficultyRange(beatmap.BeatmapInfo.BaseDifficulty.ApproachRate, 1800, 1200, 450) / clockRate;

            int maxCombo = beatmap.HitObjects.Count;
            // Add the ticks + tail of the slider. 1 is subtracted because the head circle would be counted twice (once for the slider itself in the line above)
            maxCombo += beatmap.HitObjects.OfType<Slider>().Sum(s => s.NestedHitObjects.Count - 1);

            int hitCirclesCount = beatmap.HitObjects.Count(h => h is HitCircle);
            int spinnerCount = beatmap.HitObjects.Count(h => h is Spinner);

            return new OsuDifficultyAttributes
            {
                StarRating = starRating,
                Mods = mods,
                AimStrain = aimRating,
                JumpAimStrain = jumpAimRating,
                FlowAimStrain = flowAimRating,
                PrecisionStrain = precisionRating,
                SpeedStrain = speedRating,
                StaminaStrain = staminaRating,
                AccuracyStrain = accuracyRating,
                ApproachRate = preempt > 1200 ? (1800 - preempt) / 120 : (1200 - preempt) / 150 + 5,
                OverallDifficulty = (80 - hitWindows.WindowFor(HitResult.Great) / clockRate) / 6,
                MaxCombo = maxCombo,
                HitCircleCount = hitCirclesCount,
                SpinnerCount = spinnerCount,
                Skills = skills
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            OsuDifficultyHitObject lastLastDifficultyObject = null;
            OsuDifficultyHitObject lastDifficultyObject = null;

            // The first jump is formed by the first two hitobjects of the map.
            // If the map has less than two OsuHitObjects, the enumerator will not return anything.
            for (int i = 1; i < beatmap.HitObjects.Count; i++)
            {
                var lastLast = i > 1 ? beatmap.HitObjects[i - 2] : null;
                var last = beatmap.HitObjects[i - 1];
                var current = beatmap.HitObjects[i];

                var difficultyHitObject = new OsuDifficultyHitObject(current, lastLast, last, lastLastDifficultyObject, lastDifficultyObject, clockRate);
                lastLastDifficultyObject = lastDifficultyObject;
                lastDifficultyObject = difficultyHitObject;
                yield return difficultyHitObject;
            }
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods) => new Skill[]
        {
            new Aim(mods),
            new RawAim(mods),
            new JumpAim(mods),
            new FlowAim(mods),
            new Speed(mods),
            new Stamina(mods),
            new RhythmComplexity(mods)
        };

        protected override Mod[] DifficultyAdjustmentMods => new Mod[]
        {
            new OsuModDoubleTime(),
            new OsuModHalfTime(),
            new OsuModEasy(),
            new OsuModHardRock(),
        };
    }
}

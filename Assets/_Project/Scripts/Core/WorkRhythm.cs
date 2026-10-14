using System;
using UnityEngine;

namespace Wyspa.Core
{
    /// <summary>
    /// Rytm pracy (GDD §11.3 B): seria skupienia (+25% Esencji po 25 min pracy bez przerwy),
    /// bonus za przerwę („odświeżony”: przerwa 5–30 min daje +15% przez 25 min) i łagodne zmęczenie
    /// (po 8 h aktywności w dobie ×0.5, po 10 h ×0.25; doba od 4:00). Gra nie nagradza przepracowania.
    /// </summary>
    public class WorkRhythm : MonoBehaviour
    {
        [SerializeField] bool rhythmEnabled = true;

        [Header("Seria skupienia")]
        [SerializeField] float streakMinutes = 25f;
        [SerializeField] float streakBonus = 0.25f;
        [Tooltip("Krótsze przerwy nie przerywają serii.")]
        [SerializeField] float gapToleranceSeconds = 120f;

        [Header("Przerwa")]
        [SerializeField] float breakMinMinutes = 5f;
        [SerializeField] float breakMaxMinutes = 30f;
        [SerializeField] float refreshedBonus = 0.15f;
        [SerializeField] float refreshedMinutes = 25f;

        [Header("Zmęczenie")]
        [SerializeField] float fatigueHours = 8f;
        [SerializeField] float fatigueMultiplier = 0.5f;
        [SerializeField] float heavyFatigueHours = 10f;
        [SerializeField] float heavyFatigueMultiplier = 0.25f;
        [SerializeField] int dayStartHour = 4;

        float streakSeconds;
        float gapSeconds;
        float idleSeconds;
        float refreshedLeft;
        float dailyActiveSeconds;
        DateTime day;

        public bool RhythmEnabled
        {
            get => rhythmEnabled;
            set => rhythmEnabled = value;
        }

        public float StreakMinutes => streakSeconds / 60f;
        public bool StreakBonusActive => streakSeconds >= streakMinutes * 60f;
        public bool Refreshed => refreshedLeft > 0f;
        public float RefreshedMinutesLeft => refreshedLeft / 60f;
        public float DailyActiveHours => dailyActiveSeconds / 3600f;

        /// <summary>0 = brak, 1 = zmęczenie, 2 = duże zmęczenie.</summary>
        public int FatigueLevel => dailyActiveSeconds >= heavyFatigueHours * 3600f ? 2 : (dailyActiveSeconds >= fatigueHours * 3600f ? 1 : 0);

        /// <summary>Premia serii skupienia (25%, ulepszenie „Seria skupienia” więcej).</summary>
        public float StreakBonus => Mathf.Max(streakBonus, Upgrades.UpgradeEffects.StreakBonus);
        /// <summary>Mnożnik Żaru przy zmęczeniu (50% / 25%, ulepszenie „Drugi oddech” łagodzi).</summary>
        public float Fatigue => Mathf.Max(fatigueMultiplier, Upgrades.UpgradeEffects.FatigueMultiplier);
        public float HeavyFatigue => Mathf.Max(heavyFatigueMultiplier, Upgrades.UpgradeEffects.HeavyFatigueMultiplier);

        public float EssenceMultiplier
        {
            get
            {
                if (!rhythmEnabled)
                    return 1f;
                float bonus = 1f + (StreakBonusActive ? StreakBonus : 0f) + (Refreshed ? refreshedBonus : 0f);
                float fatigue = FatigueLevel == 2 ? HeavyFatigue : (FatigueLevel == 1 ? Fatigue : 1f);
                return bonus * fatigue;
            }
        }

        /// <summary>Wywoływane co tick przez GameLoop. now = prawdziwy czas (granica doby).</summary>
        public void Tick(bool active, float dt, DateTime now)
        {
            var today = DayOf(now);
            if (today != day)
            {
                day = today;
                dailyActiveSeconds = 0f;
            }

            if (active)
            {
                float idleMin = idleSeconds / 60f;
                if (idleMin >= breakMinMinutes && idleMin <= breakMaxMinutes)
                    refreshedLeft = refreshedMinutes * 60f;     // powrót z udanej przerwy
                idleSeconds = 0f;
                gapSeconds = 0f;
                streakSeconds += dt;
                dailyActiveSeconds += dt;
                refreshedLeft = Mathf.Max(0f, refreshedLeft - dt);
            }
            else
            {
                idleSeconds += dt;
                gapSeconds += dt;
                if (gapSeconds > gapToleranceSeconds)
                    streakSeconds = 0f;
            }
        }

        DateTime DayOf(DateTime t) => t.AddHours(-dayStartHour).Date;

        public long SaveDayTicks => day.Ticks;
        public float SaveDailySeconds => dailyActiveSeconds;

        public void Restore(long dayTicks, float dailySeconds, DateTime now)
        {
            if (dayTicks <= 0)
                return;
            var saved = new DateTime(dayTicks);
            if (saved == DayOf(now))
            {
                day = saved;
                dailyActiveSeconds = Mathf.Max(0f, dailySeconds);
            }
        }
    }
}

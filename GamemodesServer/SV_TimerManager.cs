﻿using System.Threading.Tasks;

namespace GamemodesServer
{
    public class TimerManager : GmScript
    {
        private static int s_secondsLeft;
        public static bool InOvertime
        {
            get
            {
                return s_secondsLeft == -1;
            }
        }

        public TimerManager()
        {
            Tick += OnTick;
        }

        private async Task OnTick()
        {
            if (s_secondsLeft > 0)
            {
                --s_secondsLeft;
            }

            TriggerClientEvent("gamemodes:cl_sv_updatetimer", s_secondsLeft);

            await Delay(1000);
        }

        public static void SetTimer(int _seconds)
        {
            s_secondsLeft = _seconds;
        }

        public static void SetOvertime()
        {
            s_secondsLeft = -1;
        }

        public static void StopTimer()
        {
            s_secondsLeft = 0;
        }

        public static bool HasTimerRunOut()
        {
            return s_secondsLeft <= 0;
        }
    }
}
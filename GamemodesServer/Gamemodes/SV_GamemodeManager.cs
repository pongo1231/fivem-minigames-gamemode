﻿using CitizenFX.Core;
using GamemodesShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GamemodesServer.Gamemodes
{
    public class GamemodeManager : GmScript
    {
        private List<Player> m_gamemodePlayers = new List<Player>();

        private static List<GamemodeScript> s_registeredGamemodes = new List<GamemodeScript>();
        private static GamemodeScript s_curGamemode = null;
        private static bool s_stopGamemode = false;

        private bool m_fullyLoadedGamemode = false;

        private Random m_random = new Random();

        private bool m_awaitingGamemodeStop = false;

        private bool m_prestartCountdownRunning = false;
        private int m_prestartCountdown = 5;

        [PlayerDropped]
        private void OnPlayerDropped(Player _player)
        {
            m_gamemodePlayers.Remove(_player);
        }

        [Tick]
        public async Task OnTickGamemodeHandle()
        {
            if (PlayerLoadStateManager.GetLoadedInPlayers().Length == 0)
            {
                if (s_curGamemode != null)
                {
                    await _StopGamemode();
                }

                return;
            }

            if (s_curGamemode == null)
            {
                m_gamemodePlayers.Clear();

                TeamManager.EnableTeams();
                while (!TeamManager.TeamsLoaded)
                {
                    await Delay(0);
                }

                s_curGamemode = s_registeredGamemodes[m_random.Next(0, s_registeredGamemodes.Count)];

                await s_curGamemode.OnPreStart();

                m_fullyLoadedGamemode = false;

                m_prestartCountdownRunning = true;
                m_prestartCountdown = 6;
            }
            else if (s_stopGamemode)
            {
                await _StopGamemode();
            }
            else
            {
                foreach (Player player in PlayerLoadStateManager.GetLoadedInPlayers())
                {
                    if (!m_gamemodePlayers.Contains(player))
                    {
                        m_gamemodePlayers.Add(player);

                        await PlayerResponseAwaiter.AwaitResponse(player, $"gamemodes:cl_sv_{s_curGamemode.EventName}_prestart", "gamemodes:sv_cl_prestartedgamemode");

                        if (!m_prestartCountdownRunning)
                        {
                            await PlayerResponseAwaiter.AwaitResponse(player, $"gamemodes:cl_sv_{s_curGamemode.EventName}_start", "gamemodes:sv_cl_startedgamemode");
                        }
                    }
                }

                if (!m_fullyLoadedGamemode)
                {
                    TriggerClientEvent("gamemodes:cl_sv_showprestartcam", s_curGamemode.Name, s_curGamemode.Description);

                    await Delay(8000);

                    TriggerClientEvent("gamemodes:cl_sv_hideprestartcam");

                    m_fullyLoadedGamemode = true;
                }

                if (TimerManager.HasRunOut && !m_prestartCountdownRunning && !m_awaitingGamemodeStop)
                {
                    m_awaitingGamemodeStop = true;

                    s_curGamemode.OnTimerUp();
                }
            }

            await Task.FromResult(0);
        }

        [Tick]
        private async Task OnTickCountdown()
        {
            if (m_prestartCountdownRunning && m_fullyLoadedGamemode)
            {
                if (m_prestartCountdown-- == 0)
                {
                    await s_curGamemode.OnStart();

                    await PlayerResponseAwaiter.AwaitResponse($"gamemodes:cl_sv_{s_curGamemode.EventName}_start", "gamemodes:sv_cl_startedgamemode");

                    m_prestartCountdownRunning = false;

                    TimerManager.SetTimer(s_curGamemode.TimerSeconds);
                }
                else
                {
                    TriggerClientEvent("gamemodes:cl_sv_setcountdowntimer", m_prestartCountdown);
                }

                await Delay(1000);
            }
        }

        private async Task _StopGamemode()
        {
            s_stopGamemode = false;
            m_awaitingGamemodeStop = false;

            TeamManager.DisableTeams();

            TimerManager.StopTimer();

            await s_curGamemode.OnPreStop();

            await PlayerResponseAwaiter.AwaitResponse($"gamemodes:cl_sv_{s_curGamemode.EventName}_prestop", "gamemodes:sv_cl_prestoppedgamemode");

            await Delay(1000);

            EPlayerTeamType winnerTeam = s_curGamemode.GetWinnerTeam();

            TriggerClientEvent("gamemodes:cl_sv_showwinnercam", (int)winnerTeam);

            await Delay(10000);

            TriggerClientEvent("gamemodes:cl_sv_hidewinnercam");

            await s_curGamemode.OnStop();

            await PlayerResponseAwaiter.AwaitResponse($"gamemodes:cl_sv_{s_curGamemode.EventName}_stop", "gamemodes:sv_cl_stoppedgamemode");

            EntityPool.ClearEntities();

            s_curGamemode = null;
        }

        public static void RegisterGamemode(GamemodeScript _gamemode)
        {
            s_registeredGamemodes.Add(_gamemode);
        }

        public static void StopGamemode()
        {
            s_stopGamemode = true;
        }
    }
}
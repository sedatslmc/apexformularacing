using System;
using System.Collections.Generic;

namespace Apex.Core
{
    public enum GameState
    {
        Boot,
        MainMenu,
        TrackSelect,
        CarSelect,
        Garage,
        Loading,
        Grid,
        Driving,
        RadioOverlay,
        Pause,
        PitApproach,
        PitStop,
        Results,
        Replay,
    }

    /// <summary>
    /// The single authoritative game state machine. Only listed transitions are
    /// allowed; <see cref="Back"/> follows a fixed back-target per state so
    /// navigation is predictable and testable.
    /// </summary>
    public sealed class GameStateMachine
    {
        static readonly Dictionary<GameState, GameState[]> Allowed = new Dictionary<GameState, GameState[]>
        {
            [GameState.Boot] = new[] { GameState.MainMenu },
            [GameState.MainMenu] = new[] { GameState.TrackSelect, GameState.Garage, GameState.Loading },
            [GameState.TrackSelect] = new[] { GameState.MainMenu, GameState.CarSelect, GameState.Loading },
            [GameState.CarSelect] = new[] { GameState.TrackSelect, GameState.Loading },
            [GameState.Garage] = new[] { GameState.MainMenu },
            [GameState.Loading] = new[] { GameState.Grid, GameState.Driving, GameState.MainMenu },
            [GameState.Grid] = new[] { GameState.Driving, GameState.Pause },
            [GameState.Driving] = new[] { GameState.Pause, GameState.RadioOverlay, GameState.PitApproach, GameState.Results, GameState.Loading },
            [GameState.RadioOverlay] = new[] { GameState.Driving, GameState.Pause, GameState.PitApproach },
            [GameState.Pause] = new[] { GameState.Driving, GameState.Grid, GameState.Loading, GameState.MainMenu },
            [GameState.PitApproach] = new[] { GameState.PitStop, GameState.Driving, GameState.Pause },
            [GameState.PitStop] = new[] { GameState.Driving, GameState.Pause },
            [GameState.Results] = new[] { GameState.MainMenu, GameState.Replay, GameState.Loading },
            [GameState.Replay] = new[] { GameState.Results },
        };

        static readonly Dictionary<GameState, GameState> BackTarget = new Dictionary<GameState, GameState>
        {
            [GameState.TrackSelect] = GameState.MainMenu,
            [GameState.CarSelect] = GameState.TrackSelect,
            [GameState.Garage] = GameState.MainMenu,
            [GameState.Driving] = GameState.Pause,
            [GameState.Grid] = GameState.Pause,
            [GameState.RadioOverlay] = GameState.Driving,
            [GameState.PitApproach] = GameState.Pause,
            [GameState.PitStop] = GameState.Pause,
            [GameState.Replay] = GameState.Results,
            [GameState.Results] = GameState.MainMenu,
        };

        public GameState Current { get; private set; } = GameState.Boot;
        /// <summary>State that Pause returns to on resume.</summary>
        public GameState ResumeTarget { get; private set; } = GameState.Driving;

        /// <summary>(from, to)</summary>
        public event Action<GameState, GameState> Changed;

        public bool CanTransition(GameState to) =>
            Allowed.TryGetValue(Current, out var list) && Array.IndexOf(list, to) >= 0;

        public bool TryTransition(GameState to)
        {
            if (!CanTransition(to)) return false;
            var from = Current;
            if (to == GameState.Pause) ResumeTarget = from == GameState.RadioOverlay ? GameState.Driving : from;
            Current = to;
            Changed?.Invoke(from, to);
            return true;
        }

        /// <summary>Resume from pause to the state that was paused.</summary>
        public bool Resume() => Current == GameState.Pause && TryTransition(ResumeTarget);

        public bool Back()
        {
            if (Current == GameState.Pause) return Resume();
            return BackTarget.TryGetValue(Current, out var target) && TryTransition(target);
        }

        /// <summary>True while a race session is loaded (car exists), paused or not.</summary>
        public bool InSession => Current == GameState.Grid || Current == GameState.Driving || Current == GameState.RadioOverlay
                                 || Current == GameState.Pause || Current == GameState.PitApproach || Current == GameState.PitStop;

        /// <summary>True when driving input may reach the car.</summary>
        public bool AcceptsDrivingInput => Current == GameState.Driving || Current == GameState.RadioOverlay
                                           || Current == GameState.PitApproach || Current == GameState.Grid;
    }
}

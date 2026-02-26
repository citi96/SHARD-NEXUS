using System;
using Shared.Models.Enums;
using Server.Configuration;

namespace Server.GameLogic
{
    public class PhaseManager
    {
        private readonly PhaseSettings _settings;
        
        public GamePhase CurrentPhase { get; private set; } = GamePhase.WaitingForPlayers;
        public float TimeRemaining { get; private set; } = -1f;
        public int CurrentRound { get; private set; } = 0;

        public event Action<GamePhase, float>? OnPhaseChanged;

        public PhaseManager(PhaseSettings settings)
        {
            _settings = settings;
        }

        public void StartGame()
        {
            CurrentRound = 1;
            TransitionTo(GamePhase.Preparation);
        }

        public void Update(float delta)
        {
            if (CurrentPhase == GamePhase.WaitingForPlayers || CurrentPhase == GamePhase.GameOver)
                return;

            if (TimeRemaining > 0)
            {
                TimeRemaining -= delta;
                if (TimeRemaining <= 0)
                {
                    OnTimerExpired();
                }
            }
        }

        private void OnTimerExpired()
        {
            switch (CurrentPhase)
            {
                case GamePhase.Preparation:
                    TransitionTo(GamePhase.Combat);
                    break;
                case GamePhase.Combat:
                    // Combat usually ends via CombatManager, but this is a safety timeout
                    // TriggerRewardPhase() will be called manually by GameServer when combats end
                    break;
                case GamePhase.Reward:
                    TransitionTo(GamePhase.MutationChoice);
                    break;
                case GamePhase.MutationChoice:
                    CurrentRound++;
                    TransitionTo(GamePhase.Preparation);
                    break;
            }
        }

        public void TriggerRewardPhase()
        {
            if (CurrentPhase == GamePhase.Combat)
            {
                TransitionTo(GamePhase.Reward);
            }
        }

        public void EndGame()
        {
            TransitionTo(GamePhase.GameOver);
        }

        private void TransitionTo(GamePhase nextPhase)
        {
            CurrentPhase = nextPhase;
            TimeRemaining = GetDurationForPhase(nextPhase);

            Console.WriteLine($"[PhaseManager] Transition to {nextPhase} (Duration: {TimeRemaining}s, Round: {CurrentRound})");
            OnPhaseChanged?.Invoke(CurrentPhase, TimeRemaining);
        }

        private float GetDurationForPhase(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.Preparation => _settings.PreparationDuration,
                GamePhase.Combat => _settings.CombatMaxDuration,
                GamePhase.Reward => _settings.RewardDuration,
                GamePhase.MutationChoice => _settings.MutationChoiceDuration,
                _ => -1f
            };
        }
    }
}

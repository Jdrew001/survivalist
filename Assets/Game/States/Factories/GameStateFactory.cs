using Assets.Game.Services.Interfaces;
using System;
using Zenject;

namespace Assets.Game.Managers.States
{
    public class GameStateFactory : IGameStateFactory
    {

        private readonly DiContainer _container;

        public GameStateFactory(DiContainer container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public DeathState CreateDeathState(GameStateManager manager)
        {
            return _container.Instantiate<DeathState>(new object[] { manager });
        }

        public GameState CreateGameState(GameStateManager manager)
        {
            return _container.Instantiate<GameState>(new object[] { manager });
        }

        public PauseState CreatePauseState(GameStateManager manager)
        {
            return _container.Instantiate<PauseState>(new object[] { manager });
        }

        public StartState CreateStartState(GameStateManager manager)
        {
            return _container.Instantiate<StartState>(new object[] { manager });
        }
    }
}

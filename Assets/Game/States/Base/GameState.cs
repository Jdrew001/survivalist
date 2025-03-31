
using Assets.Game.Services.Interfaces;
using Zenject;

namespace Assets.Game.Managers
{
    public abstract class GameState
    {
        protected GameStateManager manager;

        [Inject]
        protected readonly ITestingService _testingService;

        public GameState(GameStateManager manager)
        {
            this.manager = manager;
        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();
    }
}
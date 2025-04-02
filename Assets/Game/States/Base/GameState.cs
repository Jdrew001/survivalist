
using Assets.Game.Services.Interfaces;
using UnityEngine;
using Zenject;

namespace Assets.Game.Managers
{
    public abstract class GameState
    {
        [Inject]
        protected GameStateManager manager;

        [Inject]
        protected readonly ITestingService _testingService;

        //[SerializeField] private string stateName;
        //public string StateName => stateName;

        public GameState()
        { }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();
    }
}
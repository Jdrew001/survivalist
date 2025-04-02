using System;
using UnityEngine;

namespace Assets.Game.Managers
{
    [Serializable]
    public class PauseState : GameState
    {
        //[SerializeField] private float difficultyLevel;
        //public float DifficultyLevel => difficultyLevel;

        public PauseState() { }

        public override void Enter()
        {
            Debug.Log("Entered PAUSE State");
            //TODO: Show pause menu -- overlay UI
            Time.timeScale = 0f; // Freeze game logic (except UI)
        }

        public override void Update()
        {
            // Check if user unpauses
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                manager.SetState(new GameplayState());
            }
        }

        public override void Exit()
        {
            Debug.Log("Exiting PAUSE State");
            // Hide pause menu
            Time.timeScale = 1f;
        }
    }
}

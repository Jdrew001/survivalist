using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Game.Managers
{
    [Serializable]
    public class GameplayState : GameState
    {
        public GameplayState() { }

        //[SerializeField] private float difficultyLevel;
        //public float DifficultyLevel => difficultyLevel;

        public override void Enter()
        {
            Debug.Log("Entered GAMEPLAY State");
            SceneManager.LoadScene("SurvivalWorld");
            Time.timeScale = 1f; 
        }

        public override void Update()
        {
            // Check for pause input
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                manager.SetState(new PauseState());
            }
        }

        public override void Exit()
        {
            Debug.Log("Exiting GAMEPLAY State");
            // Cleanup if needed
        }
    }
}

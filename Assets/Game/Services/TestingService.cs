using Assets.Game.Services.Interfaces;
using UnityEngine;

public class TestingService : ITestingService
{
    public void showMessage(string message)
    {
        Debug.Log($"TestingService: {message}");
    }
}

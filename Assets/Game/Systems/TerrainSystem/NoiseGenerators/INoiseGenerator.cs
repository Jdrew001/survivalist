using UnityEngine;

/// <summary>
/// Interface for noise generation strategies.
/// Adheres to Interface Segregation Principle by defining a minimal, focused interface.
/// </summary>
public interface INoiseGenerator
{
    /// <summary>
    /// Generates a noise value at the specified position.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>Noise value between 0 and 1</returns>
    float GenerateNoise(int x, int y);

    /// <summary>
    /// Sets the scale for this noise generator
    /// </summary>
    /// <param name="scale">The scale value to use</param>
    void SetScale(float scale);

    /// <summary>
    /// Sets the seed for the noise generator
    /// </summary>
    /// <param name="seed">The random seed</param>
    void SetSeed(int seed);
}
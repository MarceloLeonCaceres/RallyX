using Microsoft.Xna.Framework;

namespace NewRallyX.Models;

public class SmogCloud
{
    public Vector2 Position { get; private set; }
    public float TimeToLive { get; private set; } // Segundos que dura el humo

    public SmogCloud(Vector2 position)
    {
        Position = position;
        TimeToLive = 3f; // En el arcade original duraba unos 3 segundos
    }

    // Método puro que resta el tiempo de vida
    public void Update(float deltaTime)
    {
        TimeToLive -= deltaTime;
    }

    public bool IsExpired => TimeToLive <= 0;
}

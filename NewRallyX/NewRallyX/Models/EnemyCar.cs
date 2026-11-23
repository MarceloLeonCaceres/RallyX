using Microsoft.Xna.Framework;
using System;

namespace NewRallyX.Models;

public class EnemyCar
{
    public Vector2 Position { get; private set; }
    public Vector2 Direction { get; private set; } = new Vector2(0, -1);
    private readonly Vector2 _startPosition;
    public float Speed { get; set; } = 75f; // Ligeramente más lento que el jugador
    private float _stunTimer = 0f;
    public bool IsStunned => _stunTimer > 0f;
    public float SpinAngle { get; private set; }

    private static readonly Vector2[] _cardinales = {
        new Vector2(0, -1), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0)
    };

    public EnemyCar(Vector2 startPosition)
    {
        _startPosition = startPosition;
        Position = startPosition;
    }

    public void Update(float deltaTime, Vector2 playerPosition, Func<Vector2, bool> esCaminoLibre)
    {
        if (IsStunned)
        {
            _stunTimer -= deltaTime;
            SpinAngle += 15f * deltaTime; // Velocidad de giro (radianes/seg)
            return; // No avanza mientras está aturdido
        }
        Vector2 siguientePos = Position + Direction * Speed * deltaTime;
        bool caminoBloqueado = !esCaminoLibre(siguientePos);

        // Decidir dirección si choca o si está alineado a la grilla de 8x8 (posible cruce)
        bool enCruce = (Math.Abs(Position.X % 8) < 1f) && (Math.Abs(Position.Y % 8) < 1f);

        if (caminoBloqueado || enCruce)
        {
            ElegirMejorDireccion(playerPosition, esCaminoLibre);
            siguientePos = Position + Direction * Speed * deltaTime;
        }

        if (esCaminoLibre(siguientePos))
        {
            Position = siguientePos;
        }
    }

    private void ElegirMejorDireccion(Vector2 objetivo, Func<Vector2, bool> esCaminoLibre)
    {
        Vector2 reversa = -Direction;
        Vector2 mejorDir = Direction;
        float menorDistancia = float.MaxValue;
        bool encontroCamino = false;

        foreach (var dir in _cardinales)
        {
            if (dir == reversa) continue; // Evitar giros en U innecesarios

            Vector2 prueba = Position + dir * 4f; // Probar 4 píxeles adelante
            if (esCaminoLibre(prueba))
            {
                float dist = Vector2.DistanceSquared(prueba, objetivo);
                if (dist < menorDistancia)
                {
                    menorDistancia = dist;
                    mejorDir = dir;
                    encontroCamino = true;
                }
            }
        }

        // Si está en un callejón sin salida, recién ahí permite dar media vuelta
        Direction = encontroCamino ? mejorDir : reversa;
    }
    public void Reset()
    {
        Position = _startPosition;
        Direction = new Vector2(0, -1);
        _stunTimer = 0f;
        SpinAngle = 0f;
    }
    public void Stun(float duration = 2.0f)
    {
        _stunTimer = duration;
    }
}

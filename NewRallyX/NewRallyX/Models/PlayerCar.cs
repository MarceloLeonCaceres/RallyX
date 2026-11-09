using Microsoft.Xna.Framework;
using System;

namespace NewRallyX.Models;

public class PlayerCar
{
    public Vector2 Position { get; private set; }
    public Vector2 Direction { get; private set; } // Vector de la grilla (ej. 1,0 para derecha)
    public float Speed { get; } = 150f; // Pixeles por segundo
                                        
    // Almacena la tecla que el jugador está presionando actualmente
    private Vector2 _nextDirection;
    public PlayerCar(Vector2 startPosition)
    {
        Position = startPosition;
        Direction = new Vector2(0,0);
        _nextDirection = Direction;
    }

    public void RequestTurn(Vector2 newDirection)
    {
        // En Rally-X, solo puedes girar 90 grados a la vez.
        _nextDirection = newDirection;
    }

    public void Move(float deltaTime, Func<Vector2, bool> caminoLibre)
    {
        // 1. Intentar el giro si el jugador presionó una nueva tecla
        if (_nextDirection != Direction && _nextDirection != Vector2.Zero)
        {
            // Snap: Si voy a moverme en Vertical (Y != 0), redondeo mi eje X al múltiplo de 8 más cercano.
            // Si voy a moverme en Horizontal (X != 0), redondeo mi eje Y.
            float snappedX = _nextDirection.Y != 0 ? (float)Math.Round(Position.X / 8.0) * 8f : Position.X;
            float snappedY = _nextDirection.X != 0 ? (float)Math.Round(Position.Y / 8.0) * 8f : Position.Y;

            Vector2 posicionAlineada = new Vector2(snappedX, snappedY);
            Vector2 pruebaDeGiro = posicionAlineada + _nextDirection * Speed * deltaTime;

            // Si doblando desde el centro del carril NO chocamos, aplicamos el giro
            if (caminoLibre(pruebaDeGiro))
            {
                Position = posicionAlineada; // Auto-alineación matemática instantánea
                Direction = _nextDirection;  // Cambiamos el vector de movimiento
            }
        }

        // 2. Continuar moviéndose en la dirección actual
        Vector2 futuraPos = Position + Direction * Speed * deltaTime;

        if (caminoLibre(futuraPos))
        {
            Position = futuraPos;
        }
        else
        {
            // LÓGICA DE VIRAJE AUTOMÁTICO (Cornering)
            // Redondeamos la posición a la grilla de 8x8 para hacer un giro limpio
            float snappedX = (float)Math.Round(Position.X / 8.0) * 8f;
            float snappedY = (float)Math.Round(Position.Y / 8.0) * 8f;
            Vector2 posAlineada = new Vector2(snappedX, snappedY);

            // Determinar cuáles son las dos direcciones perpendiculares a mi movimiento actual
            Vector2 dirPerpendicular1 = Direction.X != 0 ? new Vector2(0, -1) : new Vector2(-1, 0);
            Vector2 dirPerpendicular2 = Direction.X != 0 ? new Vector2(0, 1) : new Vector2(1, 0);

            // Probar un pequeño paso matemático en esas direcciones
            bool libre1 = caminoLibre(posAlineada + dirPerpendicular1 * 8f);
            bool libre2 = caminoLibre(posAlineada + dirPerpendicular2 * 8f);

            // Si solo hay un camino disponible (es una esquina en forma de L), forzamos el giro
            if (libre1 && !libre2)
            {
                Position = posAlineada;
                Direction = dirPerpendicular1;
                _nextDirection = dirPerpendicular1;
            }
            else if (libre2 && !libre1)
            {
                Position = posAlineada;
                Direction = dirPerpendicular2;
                _nextDirection = dirPerpendicular2;
            }
            // Nota: Si ambas están libres (una intersección en T) o ambas bloqueadas (callejón sin salida),
            // el auto simplemente se detendrá esperando que presiones una tecla.
        }
    }
}

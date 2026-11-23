using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NewRallyX.Framework;
using NewRallyX.Framework.Sprites;
using NewRallyX.Models;
using NewRallyX.SpriteSheets;

namespace NewRallyX.Components;

internal class Sidebar : GameComponent<NewRallyXGame>
{
    private const int LEFT = 224;
    private const int TOP = 0;
    private const int SIDEBAR_WIDTH = 64;
    private const int SCREEN_HEIGHT = 224;
    private const int RADAR_HEIGHT = 112;

    private readonly Texture2D _texture;
    private readonly Texture2D _fuelGauge;
    private readonly ArcadeFont _font;
    private readonly SpriteSheet _sprites;
    private readonly SpriteBatch _batch;
    // para el radar, necesitamos un sprite para cada celda del radar, y un sprite para el fondo del radar
    private Texture2D _pixel;
    private Rectangle _radarArea = new Rectangle(720, 220, 120, 192);

    public Sidebar(Game game, SpriteBatch spriteBatch, SpriteSheet sprites, ArcadeFont font)
        : base(game)
    {
        _texture = new Texture2D(game.GraphicsDevice, 1, 1);
        _fuelGauge = game.Content.Load<Texture2D>("fuel-indicator");
        _sprites = sprites;
        _font = font;
        _texture.SetData(new Color[] { Color.White });
        _batch = spriteBatch;
    }

    public override void Draw(GameTime gameTime)
    {
        // Background
        Rectangle sidebarBackground = new Rectangle(LEFT, TOP, SIDEBAR_WIDTH, SCREEN_HEIGHT);
        _batch.Draw(_texture, sidebarBackground, Color.Black);

        // Static text
        Print("HI-SCORE", 0, gameTime, ArcadeFont.White);
        Print($"{Game.GameState.Player}UP", 16, gameTime, ArcadeFont.White);
        Print($"ROUND{Game.GameState.Round,3}", 208, gameTime, ArcadeFont.White);

        // Fuel
        // 1. Calculamos el porcentaje actual de gasolina (de 0.0 a 1.0)
        float porcentaje = (float)Game.GameState.Fuel[Game.GameState.Player - 1] / GameState.INITIAL_FUEL;
        int width = (int)(64 * porcentaje);

        _batch.Draw(_fuelGauge, new Vector2(LEFT, 56), Color.White);
        Rectangle fuelRemaining = new Rectangle(LEFT + (64 - width), 65, width, 6);
        _batch.Draw(_texture, fuelRemaining, ArcadeFont.Yellow);

        // Dynamic text
        Print($"{Game.GameState.HiScore,8}", 8, gameTime, ArcadeFont.Red);
        Print($"{Game.GameState.Scores[Game.GameState.Player - 1],8}", 24, gameTime, ArcadeFont.Cyan);

        // Lives
        for (int i = 0; i < Game.GameState.Lives[Game.GameState.Player - 1] - 1; i++)
        {
            _sprites.Draw(_batch, new Vector2(LEFT + i * 16, 192), 8, gameTime, Color.White);
        }

        // Radar
        Rectangle radarBackground = new Rectangle(LEFT, 80, SIDEBAR_WIDTH, RADAR_HEIGHT);
        _batch.Draw(_texture, radarBackground, ArcadeFont.RadarBlue);
        DibujarRadar(radarBackground, Game.PlayerPosition);

        base.Draw(gameTime);
    }

    private void Print(string message, int offset, GameTime gameTime, Color colour)
    {
        Vector2 pos = new Vector2(LEFT, offset);
        _font.Draw(_batch, pos, message, gameTime, colour);
    }
    private void DibujarRadar(Rectangle radarArea, Vector2 playerPosition)
    {
        float escalaX = radarArea.Width / 120f;
        float escalaY = radarArea.Height / 192f;

        // 1. Banderas
        for (int y = 0; y < 192; y++)
        {
            for (int x = 0; x < 120; x++)
            {
                int tileId = MapShape.Map123[(y * 120) + x];

                if (tileId == NewRallyXGame.ID_BANDERA || tileId == NewRallyXGame.ID_BANDERA_ESPECIAL)
                {
                    int rx = radarArea.X + (int)(x * escalaX);
                    int ry = radarArea.Y + (int)(y * escalaY);
                    Color colorBandera = (tileId == NewRallyXGame.ID_BANDERA_ESPECIAL) ? ArcadeFont.Cyan : ArcadeFont.Yellow;

                    _batch.Draw(_texture, new Rectangle(rx, ry, 2, 2), colorBandera);
                }
            }
        }

        // 2. Jugador
        int px = radarArea.X + (int)((playerPosition.X / 8f) * escalaX);
        int py = radarArea.Y + (int)((playerPosition.Y / 8f) * escalaY);

        // Enemigos (Puntos rojos)
        foreach (var enemy in Game.Enemies)
        {
            int ex = radarArea.X + (int)((enemy.Position.X / 8f) * escalaX);
            int ey = radarArea.Y + (int)((enemy.Position.Y / 8f) * escalaY);
            _batch.Draw(_texture, new Rectangle(ex, ey, 2, 2), ArcadeFont.Red);
        }

        _batch.Draw(_texture, new Rectangle(px, py, 2, 2), ArcadeFont.White);
    }
}

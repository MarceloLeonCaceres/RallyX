using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using NewRallyX.Components;
using NewRallyX.Framework.Sprites;
using NewRallyX.Models;
using NewRallyX.SpriteSheets;
using System.Collections.Generic;
using System.Linq;

namespace NewRallyX;

public class NewRallyXGame : Game
{
    private const int GAME_WIDTH = 288 * 4;
    private const int GAME_HEIGHT = 224 * 4;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private Texture2D _blocks;
    private Texture2D _font;
    private Texture2D _spriteTex;
    private Texture2D _fuelBarTex;
    private Texture2D _mapBlocksTex;
    private Texture2D _debugHitbox;

    private SpriteSheet _smallBlocks;
    private SpriteSheet _largeBlocks;
    private SpriteSheet _fontSheet;
    private SpriteSheet _sprites;
    private ArcadeFont _arcadeFont;
    private SpriteSheet _fuelBar;
    private SpriteSheet _mapBlocks;

    private Vector2 _offset = new Vector2(0, 0);


    private Sidebar _sidebar;
    private PlayerCar _player;
    private List<SmogCloud> _smogClouds = new List<SmogCloud>();
    private KeyboardState _previousKeyboardState;

    internal GameState GameState { get; private set; } = new GameState();
    private Song _musicaFondo;
    private SoundEffect _efectoHumo;
    private SoundEffect _efectoCrash;
    private const int ID_ROCA = 99;

    public NewRallyXGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _graphics.PreferredBackBufferHeight = GAME_HEIGHT;
        _graphics.PreferredBackBufferWidth = GAME_WIDTH;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here
        // Coordenada inicial de prueba
        _player = new PlayerCar(new Vector2(456, 1200));

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _debugHitbox = new Texture2D(GraphicsDevice, 1, 1);
        _debugHitbox.SetData(new Color[] { Color.White });

        _blocks = Content.Load<Texture2D>("blocks");
        _largeBlocks = new SpriteSheet(_blocks, 4, 4);
        _smallBlocks = new SpriteSheet(_blocks, 12, 12);

        _font = Content.Load<Texture2D>("arcade-font");
        _fontSheet = new SpriteSheet(_font, 22, 2);
        _arcadeFont = new ArcadeFont(_fontSheet);

        _spriteTex = Content.Load<Texture2D>("sprites");
        _sprites = new SpriteSheet(_spriteTex, 11, 1);

        _fuelBarTex = Content.Load<Texture2D>("fuel-bar");
        _fuelBar = new SpriteSheet(_fuelBarTex, 8, 1);

        _mapBlocksTex = Content.Load<Texture2D>("map-blocks");
        _mapBlocks = new SpriteSheet(_mapBlocksTex, 21, 96);

        _sidebar = new Sidebar(this, _spriteBatch, _sprites, _arcadeFont);

        _musicaFondo = Content.Load<Song>("03. NEW RALLY X - IN GAME MUSIC");
        _efectoHumo = Content.Load<SoundEffect>("05. NEW RALLY X - MY CAR -SMOKESCREEN");
        _efectoCrash = Content.Load<SoundEffect>("30. RALLY X - MY CAR -CRASH");
        Components.Add(_sidebar);

        MediaPlayer.IsRepeating = true;
        MediaPlayer.Play(_musicaFondo);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        KeyboardState key = Keyboard.GetState();

        // 1. Enviar intención de giro al dominio
        if (key.IsKeyDown(Keys.W)) _player.RequestTurn(new Vector2(0, -1));
        else if (key.IsKeyDown(Keys.S)) _player.RequestTurn(new Vector2(0, 1));
        else if (key.IsKeyDown(Keys.A)) _player.RequestTurn(new Vector2(-1, 0));
        else if (key.IsKeyDown(Keys.D)) _player.RequestTurn(new Vector2(1, 0));

        // 2. Mover matemáticamente el auto
        // Dentro de NewRallyXGame.Update()
        _player.Move(deltaTime, EsCaminoLibre);

        if (PisoUnaRoca())
            ManejarMuerteJugador();

        // 3. Centrar la cámara (invirtiendo la posición del auto y sumando la mitad de la pantalla 288x224)
        _offset = new Vector2(-_player.Position.X + 144, -_player.Position.Y + 112);

        // LÓGICA DEL SMOG
        // 1. Detectar si se presionó la tecla de Espacio en este fotograma exacto
        if (key.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space))
        {
            // 2. Verificar que tengamos combustible suficiente (ej. cuesta 2 puntos de fuel)
            if (GameState.Fuel[0] > 2)
            {
                GameState.Fuel[0] -= 2; // Restamos el costo del combustible

                // Creamos el humo exactamente donde está el auto ahora mismo
                _smogClouds.Add(new SmogCloud(_player.Position));
                _efectoHumo.Play(); // Reproducimos el efecto de sonido del humo
            }
        }

        // 3. Actualizar la disipación del humo (contadores de tiempo)
        for (int i = _smogClouds.Count - 1; i >= 0; i--)
        {
            _smogClouds[i].Update(deltaTime);

            if (_smogClouds[i].IsExpired)
            {
                _smogClouds.RemoveAt(i); // Lo borramos de la memoria al desaparecer
            }
        }

        _previousKeyboardState = key; // Guardamos el estado para el próximo fotograma
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Render to target
        using (RenderTarget2D target = new RenderTarget2D(GraphicsDevice, 288, 224))
        {
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _spriteBatch.Begin(SpriteSortMode.BackToFront, samplerState: SamplerState.PointClamp);

            // Dibuja las nubes de humo
            foreach (var smog in _smogClouds)
            {
                // Nota: Reemplaza el '4' por el ID visual que le corresponda al humo en tus sprites
                _largeBlocks.Draw(_spriteBatch, smog.Position + _offset, 8, gameTime, Color.LightGray);
            }

            // ... (Aquí debajo ya tienes el código que dibuja a _player) ...
            for (int y = 0; y < 192; y++)
            {
                for (int x = 0; x < 120; x++)
                {
                    int index = (y * 120) + x;
                    int spriteID = MapShape.Map123[index];
                    _mapBlocks.Draw(_spriteBatch, _offset + new Vector2(x * 8, y * 8), spriteID, gameTime, Color.White);
                }
            }
            var spriteData = ObtenerSprite(_player.Direction);

            // Se pasa 0f para la rotación (no la necesitamos) y el efecto calculado al final.
            _sprites.Draw(_spriteBatch, _player.Position + _offset, spriteData.id, gameTime, Color.White, 0f, spriteData.efecto);

            // Mismos márgenes que usamos en EsCaminoLibre
            int margen = 2;
            int tamanoCaja = 12;

            // Calcular el rectángulo exacto en pantalla sumando el offset de la cámara
            Rectangle hitboxVisual = new Rectangle(
                (int)(_player.Position.X + margen + _offset.X),
                (int)(_player.Position.Y + margen + _offset.Y),
                tamanoCaja,
                tamanoCaja
            );

            // Dibujar el cuadro con color Rojo al 50% de transparencia
            _spriteBatch.Draw(_debugHitbox, hitboxVisual, Color.Red * 0.5f);

            _spriteBatch.End();

            // Expand target to full window
            GraphicsDevice.SetRenderTarget(null);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _spriteBatch.Draw(target, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.White);
            
            base.Draw(gameTime);
            
            _spriteBatch.End();
        }
    }

    private (int id, SpriteEffects efecto) ObtenerSprite(Vector2 direccion)
    {
        // Hacia Arriba: ID 0 original
        if (direccion == new Vector2(0, -1))
            return (0, SpriteEffects.None);

        // Hacia la Derecha: ID 3 original
        if (direccion == new Vector2(1, 0))
            return (3, SpriteEffects.None);

        // Hacia la Izquierda: ID 3 (Derecha) + Espejo Horizontal
        if (direccion == new Vector2(-1, 0))
            return (3, SpriteEffects.FlipHorizontally);

        // Hacia Abajo: ID 0 (Arriba) + Espejo Vertical
        if (direccion == new Vector2(0, 1))
            return (0, SpriteEffects.FlipVertically);

        return (0, SpriteEffects.None); // Por defecto
    }
    private bool EsCaminoLibre(Vector2 futuraPos)
    {
        int margen = 2;
        int tamanoCaja = 12;

        Vector2[] esquinas = {
        new Vector2(futuraPos.X + margen, futuraPos.Y + margen),
        new Vector2(futuraPos.X + margen + tamanoCaja, futuraPos.Y + margen),
        new Vector2(futuraPos.X + margen, futuraPos.Y + margen + tamanoCaja),
        new Vector2(futuraPos.X + margen + tamanoCaja, futuraPos.Y + margen + tamanoCaja)
    };

        // Lista de IDs que el auto PUEDE pisar (0 = asfalto, 24/25/32 = camino punteado/sombras)
        int[] tilesCaminables = { 0, 24, 25, 32, 3, 4, 11 };

        foreach (var esquina in esquinas)
        {
            int tileX = (int)(esquina.X / 8);
            int tileY = (int)(esquina.Y / 8);

            if (tileX < 0 || tileX >= 120 || tileY < 0 || tileY >= 192) return false;

            int index = (tileY * 120) + tileX;
            int tileId = MapShape.Map123[index];

            // Si el tile actual NO está en la lista de caminables, es una pared
            if (!tilesCaminables.Contains(tileId))
            {
                return false;
            }
        }

        return true;
    }
    private bool PisoUnaRoca()
    {
        // Evaluamos el centro del auto (sumando 4px a la posición de la esquina superior izquierda)
        int tileX = (int)((_player.Position.X + 4) / 8);
        int tileY = (int)((_player.Position.Y + 4) / 8);

        // Evitar desbordes si cruzamos los túneles
        if (tileX < 0 || tileX >= 120 || tileY < 0 || tileY >= 192) return false;

        int index = (tileY * 120) + tileX;
        return MapShape.Map123[index] == ID_ROCA;
    }
    private void ManejarMuerteJugador()
    {
        _efectoCrash.Play();
        GameState.Lives[0]--;

        if (GameState.Lives[0] > 0)
        {
            // Reiniciar la posición a la zona segura y limpiar el humo
            _player = new PlayerCar(new Vector2(32, 112));
            _smogClouds.Clear();
        }
        else
        {
            // TODO: Lógica de Game Over (volver al menú principal, etc.)
        }
    }
}
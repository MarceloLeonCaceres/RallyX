using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using NewRallyX.Components;
using NewRallyX.Framework.Sprites;
using NewRallyX.Models;
using NewRallyX.SpriteSheets;
using System;
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
    //private Texture2D _debugHitbox;

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
    private SoundEffect _efectoBanderaNormal;
    private SoundEffect _efectoBanderaEspecial;
    private SoundEffect _efectoCombustible;
    private const int ID_ROCA = 99;
    private Vector2 _direccionVisual = new Vector2(1, 0); // Asumiendo que arranca a la derecha
    private int _frameGiro = 0; // 0 = Recto, 1 = Giro intermedio, 2 = Casi 90°
    private float _timerAnimacion = 0f;
    private bool _giraDerecha = true;
    // Cambia el 10 por el ID real de la bandera amarilla en tu matriz
    public const int ID_BANDERA = 900;
    public const int ID_BANDERA_ESPECIAL = 901;
    private const int PUNTOS_POR_BANDERA = 100;

    // Como subiste tu tanque a 6400, recargar 1 punto no se notará. Le daremos 500.
    private const int COMBUSTIBLE_POR_BANDERA = 500;
    public Vector2 PlayerPosition => _player.Position;
    // carros enemigos
    private List<EnemyCar> _enemies = new List<EnemyCar>();
    public IReadOnlyList<EnemyCar> Enemies => _enemies;
    private float _crashTimer = 0f;
    private bool EstaChocando => _crashTimer > 0f;
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
        SembrarBanderas();
        _enemies.Add(new EnemyCar(new Vector2(456, 1264)));
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // _debugHitbox = new Texture2D(GraphicsDevice, 1, 1);
        // _debugHitbox.SetData(new Color[] { Color.White });

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
        _efectoBanderaNormal = Content.Load<SoundEffect>("06. NEW RALLY X - NORMAL FLAG -GET");
        _efectoBanderaEspecial = Content.Load<SoundEffect>("07. NEW RALLY X - SPECIAL FLAG -GET");
        _efectoCombustible = Content.Load<SoundEffect>("09. NEW RALLY X - FUEL BONUS SOUND");
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

        if (EstaChocando)
        {
            _crashTimer -= deltaTime;
            if (_crashTimer <= 0f)
            {
                CompletarReinicio();
            }
            return; // Congela autos, humo y gasolina durante los 1.5s
        }

        // 1. Enviar intención de giro al dominio
        if (key.IsKeyDown(Keys.Up)) _player.RequestTurn(new Vector2(0, -1));
        else if (key.IsKeyDown(Keys.Down)) _player.RequestTurn(new Vector2(0, 1));
        else if (key.IsKeyDown(Keys.Left)) _player.RequestTurn(new Vector2(-1, 0));
        else if (key.IsKeyDown(Keys.Right)) _player.RequestTurn(new Vector2(1, 0));

        // 2. Mover matemáticamente el auto
        // Dentro de NewRallyXGame.Update()
        _player.Move(deltaTime, EsCaminoLibre);

        if(PisoUnaRoca() || ChocoConEnemigo())
        {
            IniciarChoque();
            return;
        }

        // 3. RECOLECTAR BANDERAS
        RecolectarBanderas();

        // 1. Detectar si la lógica dobló, pero la visual aún no
        if (_player.Direction != _direccionVisual && _player.Direction != Vector2.Zero && _frameGiro == 0)
        {
            // Producto cruz en 2D para saber si gira en sentido horario o antihorario
            float productoCruz = (_direccionVisual.X * _player.Direction.Y) - (_direccionVisual.Y * _player.Direction.X);

            if (productoCruz != 0) // Si no es un giro de 180° (frenazo en reversa)
            {
                _giraDerecha = productoCruz > 0;
                _frameGiro = 1;          // Disparamos el estado de animación
                _timerAnimacion = 0.05f; // 50ms por cada frame intermedio (muy rápido)
            }
            else
            {
                _direccionVisual = _player.Direction; // Actualización instantánea
            }
        }

        // 2. Ejecutar el temporizador del estado
        if (_frameGiro > 0)
        {
            _timerAnimacion -= deltaTime;
            if (_timerAnimacion <= 0)
            {
                _frameGiro++;
                _timerAnimacion = 0.05f;

                if (_frameGiro > 2) // Terminó la secuencia
                {
                    _frameGiro = 0;
                    _direccionVisual = _player.Direction; // La visual alcanzó a la lógica
                }
            }
        }
        else if (_player.Direction != Vector2.Zero)
        {
            // Si no está girando, aseguramos que apunte a donde va
            _direccionVisual = _player.Direction;
        }
        
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

        // Actualizar carros enemigos
        foreach (var enemy in _enemies)
            enemy.Update(deltaTime, _player.Position, EsCaminoLibre);

        _previousKeyboardState = key; // Guardamos el estado para el próximo fotograma

        // Revisar si los enemigos chocaron con el humo
        foreach (var enemy in _enemies)
        {
            if (enemy.IsStunned) continue;

            Rectangle enemyHitbox = new Rectangle((int)enemy.Position.X + 2, (int)enemy.Position.Y + 2, 12, 12);

            foreach (var smog in _smogClouds)
            {
                Rectangle smogHitbox = new Rectangle((int)smog.Position.X, (int)smog.Position.Y, 16, 16);

                if (enemyHitbox.Intersects(smogHitbox))
                {
                    enemy.Stun(2.5f); // Aturdido por 2.5 segundos
                    break;
                }
            }
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // Render to target 
        // (Nota: Crear el RenderTarget aquí cada frame consume mucha RAM. 
        // A futuro será mejor mover este 'new RenderTarget2D' a tu LoadContent).
        using (RenderTarget2D target = new RenderTarget2D(GraphicsDevice, 288, 224))
        {
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _spriteBatch.Begin(SpriteSortMode.BackToFront, samplerState: SamplerState.PointClamp);

            // 1. DIBUJAR EL MAPA Y LAS BANDERAS (Capa del fondo)
            for (int y = 0; y < 192; y++)
            {
                for (int x = 0; x < 120; x++)
                {
                    int index = (y * 120) + x;
                    int spriteID = MapShape.Map123[index];

                    if (spriteID == ID_BANDERA)
                    {
                        Rectangle flagSourceRect = new Rectangle(144, 0, 16, 16);
                        // Restamos 4 a X y a Y para centrarla en la baldosa
                        Vector2 posicionBandera = _offset + new Vector2((x * 8) - 4, (y * 8) - 4);
                        _spriteBatch.Draw(_sprites.Texture, posicionBandera, flagSourceRect, Color.White);
                    }
                    else if (spriteID == ID_BANDERA_ESPECIAL)
                    {
                        Rectangle specialFlagSourceRect = new Rectangle(160, 0, 16, 16);
                        Vector2 posicionBandera = _offset + new Vector2((x * 8) - 4, (y * 8) - 4);
                        _spriteBatch.Draw(_sprites.Texture, posicionBandera, specialFlagSourceRect, Color.White);
                    }
                    else if (spriteID != 0) // Si es pared, se dibuja normal
                    {
                        _mapBlocks.Draw(_spriteBatch, _offset + new Vector2(x * 8, y * 8), spriteID, gameTime, Color.White);
                    }
                }
            }

            // 2. DIBUJAR EL HUMO (Capa media)
            foreach (var smog in _smogClouds)
            {
                _largeBlocks.Draw(_spriteBatch, smog.Position + _offset, 8, gameTime, Color.LightGray);
            }

            // 3. DIBUJAR EL AUTO (Capa superior)
            // A. Calcular rotación base hacia dónde va el auto
            float rotacionBase = (float)Math.Atan2(_direccionVisual.Y, _direccionVisual.X);

            // B. Compensar los 90 grados porque el dibujo original mira hacia arriba
            rotacionBase += MathHelper.PiOver2;

            // C. Extraer el cuadro de animación correspondiente (0, 1 o 2)
            Rectangle sourceRect = new Rectangle(_frameGiro * 16, 0, 16, 16);
            if (EstaChocando)
            {
                // Tiempo transcurrido desde el comienzo del choque.
                float tiempoTranscurrido = 1.5f - _crashTimer;

                // Giro continuo durante el choque.
                // 4 vueltas completas en 1.5 segundos.
                float anguloChoque = tiempoTranscurrido * MathHelper.TwoPi * 4f;

                _spriteBatch.Draw(
                    _sprites.Texture,
                    _player.Position + _offset + new Vector2(8, 8),
                    sourceRect,
                    Color.White,
                    anguloChoque,
                    new Vector2(8, 8),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }
            else
            {
                _spriteBatch.Draw(
                    _sprites.Texture,
                    _player.Position + _offset + new Vector2(8, 8),
                    sourceRect,
                    Color.White,
                    rotacionBase,
                    new Vector2(8, 8),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }
            //if (EstaChocando)
            //{
            //    // Alterna entre 2 frames de explosión cada 0.15s (ajusta los índices X, Y de tu sprites.png)
            //    int frameExplosion = ((int)(_crashTimer * 10) % 2 == 0) ? 12 : 13;
            //    Rectangle crashRect = new Rectangle(frameExplosion * 16, 0, 16, 16);

            //    _spriteBatch.Draw(
            //        _sprites.Texture,
            //        _player.Position + _offset,
            //        crashRect,
            //        Color.White
            //    );
            //}
            //else
            //{
            //    // D. Dibujar rotando desde el centro geométrico (8, 8)
            //    _spriteBatch.Draw(
            //        _sprites.Texture,
            //        _player.Position + _offset + new Vector2(8, 8),
            //        sourceRect,
            //        Color.White,
            //        rotacionBase,
            //        new Vector2(8, 8),  // Centro de rotación
            //        1f,                 // Escala
            //        SpriteEffects.None, // La rotación pura ya hace todo el trabajo
            //        0f
            //    );
            //}

            // DIBUJAR ENEMIGOS (Autos rojos)
            // Ajusta 'indiceAutoRojo' (columna) y 'filaAutoRojo' (fila) según tu sprites.png
            int indiceAutoRojo = 4;
            int filaAutoRojo = 0;
            Rectangle enemySourceRect = new Rectangle(indiceAutoRojo * 16, filaAutoRojo * 16, 16, 16);

            foreach (var enemy in _enemies)
            {
                float rotacionEnemigo = enemy.IsStunned
                    ? enemy.SpinAngle
                    : (float)Math.Atan2(enemy.Direction.Y, enemy.Direction.X) + MathHelper.PiOver2;

                _spriteBatch.Draw(
                    _sprites.Texture,
                    enemy.Position + _offset + new Vector2(8, 8),
                    enemySourceRect,
                    Color.White,
                    rotacionEnemigo,
                    new Vector2(8, 8),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }

            _spriteBatch.End();

            // 4. PASAR EL BUFFER A LA PANTALLA PRINCIPAL
            GraphicsDevice.SetRenderTarget(null);
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            _spriteBatch.Draw(target, new Rectangle(0, 0, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight), Color.White);

            // 5. DIBUJAR LA INTERFAZ DE USUARIO (El Sidebar negro)
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
            int tileX = (int)Math.Floor(esquina.X / 8.0f);
            int tileY = (int)Math.Floor(esquina.Y / 8.0f);

            // Si el auto intenta salir de los límites de la matriz (0 a 119 en X, 0 a 191 en Y), choca.
            if (tileX < 0 || tileX >= 120 || tileY < 0 || tileY >= 192) return false;

            int index = (tileY * 120) + tileX;
            int tileId = MapShape.Map123[index];

            // El asfalto es estrictamente el 0. Todo lo demás es pared u obstáculo.
            // CORRECCIÓN: El asfalto (0) y las banderas son caminables
            if (tileId != 0 && tileId != ID_BANDERA && tileId != ID_BANDERA_ESPECIAL)
            {
                return false; // Choca
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
        GameState.Lives[0]--;

        if (GameState.Lives[0] <= 0)
        {

            // TODO: Lógica de Game Over (volver al menú principal, etc.)
            return;
        }
               
        _player.Reset();
        // Reiniciar la posición a la zona segura y limpiar el humo
        //_player = new PlayerCar(new Vector2(32, 112));
        _direccionVisual = _player.Direction;
        _frameGiro = 0;

        foreach (var enemy in _enemies)
        {
            enemy.Reset();
        }
        
        _smogClouds.Clear();
    }
    private void IniciarChoque()
    {
        _crashTimer = 1.5f;
        _efectoCrash.Play();
    }
    private void CompletarReinicio()
    {
        int jugadorActual = GameState.Player - 1;
        GameState.Lives[jugadorActual]--;

        if (GameState.Lives[jugadorActual] <= 0)
        {
            // Lógica de Game Over
            return;
        }

        _player.Reset();
        _direccionVisual = _player.Direction;
        _frameGiro = 0;

        foreach (var enemy in _enemies)
            enemy.Reset();

        _smogClouds.Clear();
    }
    private void RecolectarBanderas()
    {
        // Evaluamos los bordes izquierdo(2)/derecho(14) y superior(2)/inferior(14) del auto
        int[] posiblesX = { (int)((_player.Position.X + 2) / 8), (int)((_player.Position.X + 14) / 8) };
        int[] posiblesY = { (int)((_player.Position.Y + 2) / 8), (int)((_player.Position.Y + 14) / 8) };

        foreach (int tX in posiblesX)
        {
            foreach (int tY in posiblesY)
            {
                if (tX < 0 || tX >= 120 || tY < 0 || tY >= 192) continue;

                int index = (tY * 120) + tX;
                int id = MapShape.Map123[index];

                if (id == ID_BANDERA || id == ID_BANDERA_ESPECIAL)
                {
                    MapShape.Map123[index] = 0; // Borrar del mapa al instante
                    _efectoBanderaNormal.Play();
                    if (id == ID_BANDERA)
                        GameState.Scores[0] += PUNTOS_POR_BANDERA;
                    else
                        GameState.Scores[0] += PUNTOS_POR_BANDERA * 2; // La especial da el doble

                    GameState.Fuel[0] = Math.Min(GameState.Fuel[0] + COMBUSTIBLE_POR_BANDERA, GameState.INITIAL_FUEL);
                }
            }
        }
    }
    
    private void SembrarBanderas()
    {
        const int ancho = 120;
        const int alto = 192;
        Random rnd = new Random();

        // Punto de partida: baldosa actual del jugador (ej. 57, 150)
        int startX = (int)(_player.Position.X / 8);
        int startY = (int)(_player.Position.Y / 8);
        int startIndex = (startY * ancho) + startX;

        bool[] visitado = new bool[MapShape.Map123.Length];
        Queue<int> cola = new Queue<int>();
        List<int> posicionesLibres = new List<int>();

        visitado[startIndex] = true;
        cola.Enqueue(startIndex);

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        // 1. Flood Fill (BFS) para encontrar solo caminos alcanzables
        while (cola.Count > 0)
        {
            int actual = cola.Dequeue();
            int x = actual % ancho;
            int y = actual / ancho;

            // Solo guardar como candidata si está en el centro del pasillo
            if (x > 0 && x < ancho - 1 && y > 0 && y < alto - 1 &&
                MapShape.Map123[actual - 1] == 0 &&
                MapShape.Map123[actual + 1] == 0 &&
                MapShape.Map123[actual - ancho] == 0 &&
                MapShape.Map123[actual + ancho] == 0)
            {
                posicionesLibres.Add(actual);
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];

                if (nx < 0 || nx >= ancho || ny < 0 || ny >= alto) continue;

                int vecino = (ny * ancho) + nx;
                if (!visitado[vecino] && MapShape.Map123[vecino] == 0)
                {
                    visitado[vecino] = true;
                    cola.Enqueue(vecino);
                }
            }
        }

        // 2. Sembrar 9 banderas normales
        for (int i = 0; i < 9 && posicionesLibres.Count > 0; i++)
        {
            int idx = rnd.Next(posicionesLibres.Count);
            MapShape.Map123[posicionesLibres[idx]] = ID_BANDERA;
            posicionesLibres.RemoveAt(idx);
        }

        // 3. Sembrar 1 bandera especial
        if (posicionesLibres.Count > 0)
        {
            int idx = rnd.Next(posicionesLibres.Count);
            MapShape.Map123[posicionesLibres[idx]] = ID_BANDERA_ESPECIAL;
        }
    }
    private bool ChocoConEnemigo()
    {
        Rectangle playerHitbox = new Rectangle(
            (int)_player.Position.X + 2, (int)_player.Position.Y + 2, 12, 12);

        foreach (var enemy in _enemies)
        {
            Rectangle enemyHitbox = new Rectangle(
                (int)enemy.Position.X + 2, (int)enemy.Position.Y + 2, 12, 12);

            if (playerHitbox.Intersects(enemyHitbox))
                return true;
        }

        return false;
    }
}
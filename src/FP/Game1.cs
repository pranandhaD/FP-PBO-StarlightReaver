using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using FP.Source.Config;
using FP.Source.Entities;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using FP.Source.States;

namespace FP;

public enum PowerUpType
{
    Health,
    Damage,
    Speed,
    RapidFire,
    MultiShot
}

public class PowerUp
{
    public PowerUpType Type { get; }
    public Rectangle Bounds { get; set; }
    public Color Color { get; }
    private const float FALL_SPEED = 2.0f;

    public PowerUp(PowerUpType type, Vector2 position)
    {
        Type = type;
        Bounds = new Rectangle((int)position.X, (int)position.Y, 15, 15);
        Color = GetColorForType(type);
    }

    private Color GetColorForType(PowerUpType type) => type switch
    {
        PowerUpType.Health => Color.Red,
        PowerUpType.Damage => Color.OrangeRed,
        PowerUpType.Speed => Color.Yellow,
        PowerUpType.RapidFire => Color.Orchid,
        PowerUpType.MultiShot => Color.LightGreen,
        _ => Color.White
    };

    public void Update()
    {
        Bounds = new Rectangle(Bounds.X, Bounds.Y + (int)FALL_SPEED, Bounds.Width, Bounds.Height);
    }
}

public class Game1 : Game
{
    private SpriteFont _gameFont;
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private KeyboardState _previousKeyboardState;
    private readonly Random _random = new();

    // Game state
    private GameState _currentState = GameState.Playing;

    // Audio variables
    private SoundEffect _shootSound;
    private SoundEffect _explosionSound;
    private Song _backgroundMusic;
    private SoundEffectInstance _shootSoundInstance;
    private SoundEffectInstance _explosionSoundInstance;

    private float _shootVolume = 1.0f;
    private float _explosionVolume = 1.0f;
    private float _musicVolume = 1.0f;

    // Pause menu
    private int _selectedMenuOption = 0;
    private readonly string[] _pauseMenuOptions = {
        "Resume",
        "Music Volume",
        "Shoot Volume",
        "Explosion Volume",
        "Return to Main Menu",
        "Exit"
    };

    // Game objects
    private Texture2D _playerTexture = null!;
    private Texture2D _bulletTexture = null!;
    private Texture2D[] _enemyTextures = new Texture2D[3];
    private Texture2D _explosionTexture = null!;
    private Rectangle _playerPosition;

    private const int MAX_HEALTH = 5;

    private readonly List<Rectangle> _playerBullets = new();
    private readonly List<Vector2> _bulletVelocities = new();
    private readonly List<Rectangle> _enemyBullets = new();
    private readonly List<Rectangle> _multiShotBullets = new();
    private readonly List<Vector2> _multiShotVelocities = new();
    private readonly List<Rectangle> _rapidFireBullets = new();
    private readonly List<Vector2> _rapidFireVelocities = new();

    private readonly List<Enemy> _enemies = new();

    private readonly List<(Rectangle position, float time)> _explosions = new();

    private float EnemySpawnDelay => Math.Max(1.0f - ((_currentLevel / 5) * 0.1f), 0.5f);

    // Game state
    private bool _isPlaying;
    private int _score;
    private int _lives;
    private int _currentLevel = 1;
    private float _enemySpawnTimer;
    private float _enemyShootTimer;

    // Power-Ups
    private readonly List<PowerUp> _powerUps = new();
    private float _playerDamageMultiplier = 1.0f;
    private float _playerSpeedMultiplier = 1.0f;
    private float _playerShootCooldown = GameConfig.PLAYER_SHOOT_COOLDOWN / 60f;
    private float _currentShootTimer = 0f;

    private bool _isMultiShotActive = false;
    private float _multiShotTimer = 0f;

    private bool _isRapidFireActive = false;
    private float _rapidFireTimer = 0f;
    private float _rapidFireCooldownMultiplier = 0.8f;

    private const float MULTI_SHOT_DURATION = 5f;
    private const float RAPID_FIRE_DURATION = 10f;
    private const float MAX_SPEED_MULTIPLIER = 2.0f;
    private const float SPEED_INCREMENT = 0.1f;

    // Power-Ups Notifications
    private List<(string message, float timer)> _notifications = new();
    private const float NOTIFICATION_DURATION = 2f; // How long notifications stay on screen

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _graphics.PreferredBackBufferWidth = GameConfig.WINDOW_WIDTH;
        _graphics.PreferredBackBufferHeight = GameConfig.WINDOW_HEIGHT;
        _graphics.ApplyChanges();
    }

    private void ResetGame()
    {
        _playerPosition = new Rectangle(
            GameConfig.WINDOW_WIDTH / 2 - 16,
            GameConfig.WINDOW_HEIGHT - 100,
            32, 32);

        _score = 0;
        _lives = GameConfig.STARTING_LIVES;
        _currentLevel = 1;
        _enemySpawnTimer = 0;
        _enemyShootTimer = 0;
        _playerBullets.Clear();
        _enemyBullets.Clear();
        _enemies.Clear();
        _explosions.Clear();
        _powerUps.Clear();
        _playerDamageMultiplier = 1.0f;
        _playerSpeedMultiplier = 1.0f;
        _playerShootCooldown = GameConfig.PLAYER_SHOOT_COOLDOWN / 60f;
        _currentShootTimer = 0f;
    }

    private Texture2D CreateBulletTexture()
    {
        Texture2D texture = new(GraphicsDevice, 4, 8);
        Color[] data = new Color[4 * 8];
        for (int i = 0; i < data.Length; i++)
            data[i] = Color.Yellow;
        texture.SetData(data);
        return texture;
    }

    private Texture2D CreateExplosionTexture()
    {
        Texture2D texture = new(GraphicsDevice, 32, 32);
        Color[] data = new Color[32 * 32];

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float distanceFromCenter = Vector2.Distance(
                    new Vector2(x, y),
                    new Vector2(16, 16)
                );

                if (distanceFromCenter < 16)
                {
                    data[y * 32 + x] = Color.Orange;
                }
                else
                {
                    data[y * 32 + x] = Color.Transparent;
                }
            }
        }

        texture.SetData(data);
        return texture;
    }

    protected override void LoadContent()
    {
        try
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load player texture
            _playerTexture = Content.Load<Texture2D>("assets/player");

            // Load enemy textures
            _enemyTextures = new Texture2D[3];
            _enemyTextures[0] = Content.Load<Texture2D>("assets/E1");
            _enemyTextures[1] = Content.Load<Texture2D>("assets/E2");
            _enemyTextures[2] = Content.Load<Texture2D>("assets/E3");

            _bulletTexture = CreateBulletTexture();
            _explosionTexture = CreateExplosionTexture();
            _gameFont = Content.Load<SpriteFont>("GameFont");

            // Load audio files (use correct extensions for SoundEffect and Song)
            _shootSound = Content.Load<SoundEffect>("songs/shoot");
            _explosionSound = Content.Load<SoundEffect>("songs/boom");
            _backgroundMusic = Content.Load<Song>("songs/GameSong");

            // Create instances for sound effects
            _shootSoundInstance = _shootSound.CreateInstance();
            _explosionSoundInstance = _explosionSound.CreateInstance();

            // Set default volumes
            _shootSoundInstance.Volume = _shootVolume;      // Use the class variable _shootVolume
            _explosionSoundInstance.Volume = _explosionVolume;

            // Start background music
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Play(_backgroundMusic);

            UpdateVolume(); // Set initial volumes
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in LoadContent: {ex.Message}");
            throw;
        }
    }

    private void UpdateVolume()
    {
        _shootSoundInstance.Volume = _shootVolume;
        _explosionSoundInstance.Volume = _explosionVolume;
        MediaPlayer.Volume = _musicVolume;
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState currentKeyboardState = Keyboard.GetState();
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // MAIN MENU: Handle Enter to start and Exit game with ESC
        if (!_isPlaying)
        {
            if (MediaPlayer.State == MediaState.Playing)
                MediaPlayer.Stop(); // Stop music on the main menu

            if (currentKeyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                Exit();
            }

            if (currentKeyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                _isPlaying = true;
                ResetGame();
                _currentState = GameState.Playing;
                MediaPlayer.Play(_backgroundMusic); // Start music when entering gameplay
            }

            _previousKeyboardState = currentKeyboardState;
            return;
        }

        // TOGGLE PAUSE STATE
        if (currentKeyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
        {
            if (_currentState == GameState.Playing)
            {
                _currentState = GameState.Paused;
                MediaPlayer.Pause();
            }
            else if (_currentState == GameState.Paused)
            {
                _currentState = GameState.Playing;
                MediaPlayer.Resume();
            }
        }

        // HANDLE PAUSE MENU LOGIC
        if (_currentState == GameState.Paused)
        {
            HandlePauseMenu(currentKeyboardState);
            _previousKeyboardState = currentKeyboardState;
            return; // Skip gameplay updates while paused
        }

        if (_currentState == GameState.Playing)
        {
            // Player movement
            if (currentKeyboardState.IsKeyDown(Keys.Left) ||
                currentKeyboardState.IsKeyDown(Keys.A))
                _playerPosition.X -= (int)(GameConfig.PLAYER_SPEED * _playerSpeedMultiplier * 60 * deltaTime);

            if (currentKeyboardState.IsKeyDown(Keys.Right) ||
                currentKeyboardState.IsKeyDown(Keys.D))
                _playerPosition.X += (int)(GameConfig.PLAYER_SPEED * _playerSpeedMultiplier * 60 * deltaTime);

            if (currentKeyboardState.IsKeyDown(Keys.Up) ||
                currentKeyboardState.IsKeyDown(Keys.W))
                _playerPosition.Y -= (int)(GameConfig.PLAYER_SPEED * _playerSpeedMultiplier * 60 * deltaTime);

            if (currentKeyboardState.IsKeyDown(Keys.Down) ||
                currentKeyboardState.IsKeyDown(Keys.S))
                _playerPosition.Y += (int)(GameConfig.PLAYER_SPEED * _playerSpeedMultiplier * 60 * deltaTime);

            // Keep player in bounds
            _playerPosition.X = MathHelper.Clamp(
                _playerPosition.X,
                0,
                GameConfig.WINDOW_WIDTH - _playerPosition.Width);
            _playerPosition.Y = MathHelper.Clamp(
                _playerPosition.Y,
                0, // Allow movement to the top of the screen
                GameConfig.WINDOW_HEIGHT - _playerPosition.Height);

            // Player shooting
            _currentShootTimer -= deltaTime;

            if (_currentShootTimer <= 0 && Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                _shootSoundInstance.Volume = _shootVolume;
                _shootSoundInstance.Play();

                if (_isMultiShotActive)
                {
                    // Left bullet moves continuously to the left
                    _multiShotBullets.Add(new Rectangle(_playerPosition.X + 4, _playerPosition.Y, 4, 8));
                    _multiShotVelocities.Add(new Vector2(-200, -400));

                    // Center bullet moves straight
                    _multiShotBullets.Add(new Rectangle(_playerPosition.X + _playerPosition.Width / 2 - 2, _playerPosition.Y, 4, 8));
                    _multiShotVelocities.Add(new Vector2(0, -400));

                    // Right bullet moves continuously to the right
                    _multiShotBullets.Add(new Rectangle(_playerPosition.X + _playerPosition.Width - 8, _playerPosition.Y, 4, 8));
                    _multiShotVelocities.Add(new Vector2(200, -400));
                }
                else if (_isRapidFireActive)
                {
                    // Faster bullets for rapid fire
                    _rapidFireBullets.Add(new Rectangle(_playerPosition.X + _playerPosition.Width / 2 - 2, _playerPosition.Y, 4, 8));
                    _rapidFireVelocities.Add(new Vector2(0, -600));
                }
                else
                {
                    // Regular straight bullets
                    _playerBullets.Add(new Rectangle(_playerPosition.X + _playerPosition.Width / 2 - 2, _playerPosition.Y, 4, 8));
                    _bulletVelocities.Add(new Vector2(0, -400));
                }
                _currentShootTimer = _playerShootCooldown;
            }

            // Update Multi-Shot Bullets
            for (int i = _multiShotBullets.Count - 1; i >= 0; i--)
            {
                Rectangle bullet = _multiShotBullets[i];
                Vector2 velocity = _multiShotVelocities[i];
                bullet.X += (int)(velocity.X * deltaTime);
                bullet.Y += (int)(velocity.Y * deltaTime);
                _multiShotBullets[i] = bullet;

                if (bullet.Y < 0 || bullet.X < 0 || bullet.X > GameConfig.WINDOW_WIDTH)
                {
                    _multiShotBullets.RemoveAt(i);
                    _multiShotVelocities.RemoveAt(i);
                }
            }

            // Update Rapid-Fire Bullets
            for (int i = _rapidFireBullets.Count - 1; i >= 0; i--)
            {
                Rectangle bullet = _rapidFireBullets[i];
                Vector2 velocity = _rapidFireVelocities[i];
                bullet.Y += (int)(velocity.Y * deltaTime);
                _rapidFireBullets[i] = bullet;

                if (bullet.Y < 0)
                {
                    _rapidFireBullets.RemoveAt(i);
                    _rapidFireVelocities.RemoveAt(i);
                }
            }
        }

        UpdateProjectiles(deltaTime);
        UpdateEnemies(deltaTime);
        UpdatePowerUps(deltaTime);
        CheckCollisions();
        UpdateExplosions(deltaTime);
        UpdateNotifications(deltaTime);

        // Game over check
        if (_lives <= 0)
            _isPlaying = false;


        _previousKeyboardState = currentKeyboardState;
        base.Update(gameTime);
    }

    private void HandlePauseMenu(KeyboardState keyboardState)
    {
        // Move up/down the menu options
        if (keyboardState.IsKeyDown(Keys.Up) && _previousKeyboardState.IsKeyUp(Keys.Up))
            _selectedMenuOption = (_selectedMenuOption - 1 + _pauseMenuOptions.Length) % _pauseMenuOptions.Length;

        if (keyboardState.IsKeyDown(Keys.Down) && _previousKeyboardState.IsKeyUp(Keys.Down))
            _selectedMenuOption = (_selectedMenuOption + 1) % _pauseMenuOptions.Length;

        // Adjust volume when selecting Music, Shoot, or Explosion volumes
        if (_pauseMenuOptions[_selectedMenuOption] == "Music Volume" ||
            _pauseMenuOptions[_selectedMenuOption] == "Shoot Volume" ||
            _pauseMenuOptions[_selectedMenuOption] == "Explosion Volume")
        {
            if (keyboardState.IsKeyDown(Keys.Left) && _previousKeyboardState.IsKeyUp(Keys.Left))
                AdjustVolume(-0.1f);
            else if (keyboardState.IsKeyDown(Keys.Right) && _previousKeyboardState.IsKeyUp(Keys.Right))
                AdjustVolume(0.1f);
        }

        // Confirm action with ENTER
        if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
        {
            switch (_pauseMenuOptions[_selectedMenuOption])
            {
                case "Resume":
                    _currentState = GameState.Playing;
                    MediaPlayer.Resume();
                    break;
                case "Return to Main Menu":
                    _isPlaying = false; // Return to main menu
                    _currentState = GameState.Playing;
                    MediaPlayer.Stop();
                    break;
                case "Exit":
                    Exit();
                    break;
            }
        }
    }

    private void AdjustVolume(float amount)
    {
        switch (_pauseMenuOptions[_selectedMenuOption])
        {
            case "Music Volume":
                _musicVolume = MathHelper.Clamp(_musicVolume + amount, 0f, 1f);
                MediaPlayer.Volume = _musicVolume;
                break;
            case "Shoot Volume":
                _shootVolume = MathHelper.Clamp(_shootVolume + amount, 0f, 1f);
                _shootSoundInstance.Volume = _shootVolume;
                break;
            case "Explosion Volume":
                _explosionVolume = MathHelper.Clamp(_explosionVolume + amount, 0f, 1f);
                _explosionSoundInstance.Volume = _explosionVolume;
                break;
        }
    }

    private void UpdateProjectiles(float deltaTime)
    {
        for (int i = _playerBullets.Count - 1; i >= 0; i--)
        {
            Rectangle bullet = _playerBullets[i];
            Vector2 velocity = _bulletVelocities[i];
            bullet.X += (int)(velocity.X * deltaTime);
            bullet.Y += (int)(velocity.Y * deltaTime);
            _playerBullets[i] = bullet;

            if (bullet.Y < 0 || bullet.X < 0 || bullet.X > GameConfig.WINDOW_WIDTH)
            {
                _playerBullets.RemoveAt(i);
                _bulletVelocities.RemoveAt(i);
            }
        }

        // Update enemy bullets
        for (int i = _enemyBullets.Count - 1; i >= 0; i--)
        {
            Rectangle bullet = _enemyBullets[i];
            bullet.Y += (int)(GameConfig.ENEMY_BULLET_SPEED * 60 * deltaTime);
            _enemyBullets[i] = bullet;

            if (bullet.Y > GameConfig.WINDOW_HEIGHT)
                _enemyBullets.RemoveAt(i);
        }
    }

    private void SpawnEnemy()
    {
        float x = _random.Next(0, GameConfig.WINDOW_WIDTH - 32);
        Vector2 position = new(x, -32);

        // Randomly select enemy texture
        Texture2D selectedTexture = _enemyTextures[_random.Next(_enemyTextures.Length)];

        _enemies.Add(new Enemy(selectedTexture, position));
    }

    private void UpdateEnemies(float deltaTime)
    {
        // Spawn enemies
        _enemySpawnTimer += deltaTime;
        if (_enemySpawnTimer >= EnemySpawnDelay)
        {
            _enemySpawnTimer = 0;
            SpawnEnemy();
        }

        // Update existing enemies
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            _enemies[i].Update();

            if (_enemies[i].Position.Y > GameConfig.WINDOW_HEIGHT)
            {
                _enemies.RemoveAt(i);
                _lives--;
            }
        }

        // Enemy shooting
        _enemyShootTimer += deltaTime;
        if (_enemyShootTimer >= GameConfig.ENEMY_SHOOT_INTERVAL * deltaTime)
        {
            _enemyShootTimer = 0;
            foreach (var enemy in _enemies)
            {
                if (_random.Next(3) == 0)
                {
                    _enemyBullets.Add(new Rectangle(
                        (int)enemy.Position.X + enemy.Width / 2 - 2,
                        (int)enemy.Position.Y + enemy.Height,
                        4, 8));
                }
            }
        }
    }

    private void UpdatePowerUps(float deltaTime)
    {
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            _powerUps[i].Update();
            if (_powerUps[i].Bounds.Y > GameConfig.WINDOW_HEIGHT)
            {
                _powerUps.RemoveAt(i);
            }
        }

        if (_isMultiShotActive)
        {
            _multiShotTimer -= deltaTime;
            if (_multiShotTimer <= 0)
            {
                _isMultiShotActive = false;
            }
        }

        if (_isRapidFireActive)
        {
            _rapidFireTimer -= deltaTime;
            if (_rapidFireTimer <= 0)
            {
                _isRapidFireActive = false;
                _playerShootCooldown = GameConfig.PLAYER_SHOOT_COOLDOWN / 60f;
            }
        }
    }

    private void UpdateExplosions(float deltaTime)
    {
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            var explosion = _explosions[i];
            explosion.time -= deltaTime;
            _explosions[i] = explosion;

            if (explosion.time <= 0)
                _explosions.RemoveAt(i);
        }
    }

    private void CheckCollisions()
    {
        // Player bullets hitting enemies
        for (int i = _playerBullets.Count - 1; i >= 0; i--)
        {
            var bullet = _playerBullets[i];
            for (int j = _enemies.Count - 1; j >= 0; j--)
            {
                var enemy = _enemies[j];
                if (bullet.Intersects(enemy.Bounds))
                {
                    _explosionSoundInstance.Volume = _explosionVolume;
                    _explosionSound.Play();

                    _playerBullets.RemoveAt(i);
                    _bulletVelocities.RemoveAt(i);
                    _enemies.RemoveAt(j);
                    _explosions.Add((enemy.Bounds, 0.3f));
                    _score += GameConfig.POINTS_PER_ENEMY;

                    // Chance to spawn power-up
                    if (_random.Next(100) < 30)
                    {
                        PowerUpType type = (PowerUpType)_random.Next(Enum.GetValues(typeof(PowerUpType)).Length);
                        _powerUps.Add(new PowerUp(type, enemy.Position));
                    }

                    UpdateLevel();
                    break;
                }
            }
        }

        // Multi-shot bullets hitting enemies
        for (int i = _multiShotBullets.Count - 1; i >= 0; i--)
        {
            var bullet = _multiShotBullets[i];
            for (int j = _enemies.Count - 1; j >= 0; j--)
            {
                var enemy = _enemies[j];
                if (bullet.Intersects(enemy.Bounds))
                {
                    _explosionSoundInstance.Volume = _explosionVolume;
                    _explosionSoundInstance.Play();

                    _multiShotBullets.RemoveAt(i);
                    _multiShotVelocities.RemoveAt(i);
                    _enemies.RemoveAt(j);
                    _explosions.Add((enemy.Bounds, 0.3f));
                    _score += GameConfig.POINTS_PER_ENEMY;

                    // Chance to spawn power-up
                    if (_random.Next(100) < 30)
                    {
                        PowerUpType type = (PowerUpType)_random.Next(Enum.GetValues(typeof(PowerUpType)).Length);
                        _powerUps.Add(new PowerUp(type, enemy.Position));
                    }

                    UpdateLevel();
                    break;
                }
            }
        }

        // Rapid-fire bullets hitting enemies
        for (int i = _rapidFireBullets.Count - 1; i >= 0; i--)
        {
            var bullet = _rapidFireBullets[i];
            for (int j = _enemies.Count - 1; j >= 0; j--)
            {
                var enemy = _enemies[j];
                if (bullet.Intersects(enemy.Bounds))
                {
                    _explosionSoundInstance.Volume = _explosionVolume;
                    _explosionSoundInstance.Play();

                    _rapidFireBullets.RemoveAt(i);
                    _rapidFireVelocities.RemoveAt(i);
                    _enemies.RemoveAt(j);
                    _explosions.Add((enemy.Bounds, 0.3f));
                    _score += GameConfig.POINTS_PER_ENEMY;

                    // Chance to spawn power-up
                    if (_random.Next(100) < 30)
                    {
                        PowerUpType type = (PowerUpType)_random.Next(Enum.GetValues(typeof(PowerUpType)).Length);
                        _powerUps.Add(new PowerUp(type, enemy.Position));
                    }

                    UpdateLevel();
                    break;
                }
            }
        }

        // Enemy bullets hitting player
        for (int i = _enemyBullets.Count - 1; i >= 0; i--)
        {
            var bullet = _enemyBullets[i];
            if (bullet.Intersects(_playerPosition))
            {
                _explosionSoundInstance.Volume = _explosionVolume;
                _explosionSound.Play();

                _enemyBullets.RemoveAt(i);
                _lives--;
                _explosions.Add((_playerPosition, 0.3f));
            }
        }

        // Enemies colliding with player
        foreach (var enemy in _enemies.ToList())
        {
            if (enemy.Bounds.Intersects(_playerPosition))
            {
                _explosionSoundInstance.Volume = _explosionVolume;
                _explosionSound.Play();

                _enemies.Remove(enemy);
                _lives--;
                _explosions.Add((_playerPosition, 0.3f));
            }
        }

        // Power-ups colliding with player
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            // Create an expanded collision area around the player
            Rectangle expandedPlayerBounds = new Rectangle(
                _playerPosition.X - 20,  // Expand 20 pixels to the left
                _playerPosition.Y - 20,  // Expand 20 pixels upward
                _playerPosition.Width + 40, // Increase width by 40 pixels
                _playerPosition.Height + 40 // Increase height by 40 pixels
            );

            if (_powerUps[i].Bounds.Intersects(expandedPlayerBounds))
            {
                if (_powerUps[i].Type == PowerUpType.Health && _lives < MAX_HEALTH)
                {
                    _lives++;
                    _notifications.Add(("Health Increased!", NOTIFICATION_DURATION));
                }
                else
                {
                    ApplyPowerUp(_powerUps[i].Type);
                }

                _powerUps.RemoveAt(i);
            }
        }
    }

    private void ApplyPowerUp(PowerUpType type)
    {
        string message = "";
        switch (type)
        {
            case PowerUpType.Health:
                if (_lives < GameConfig.STARTING_LIVES)
                {
                    _lives++;
                    message = "Health Up!";
                }
                break;
            case PowerUpType.Damage:
                _playerDamageMultiplier += 0.5f;
                message = "Damage Increased!";
                break;
            case PowerUpType.Speed:
                if (_playerSpeedMultiplier < MAX_SPEED_MULTIPLIER)
                {
                    _playerSpeedMultiplier = Math.Min(_playerSpeedMultiplier + SPEED_INCREMENT, MAX_SPEED_MULTIPLIER);
                    message = $"Speed Boost! Now x{_playerSpeedMultiplier:F1}";
                }
                break;
            case PowerUpType.RapidFire:
                _isRapidFireActive = true;
                _rapidFireTimer = RAPID_FIRE_DURATION;
                _playerShootCooldown = GameConfig.PLAYER_SHOOT_COOLDOWN / 60f * _rapidFireCooldownMultiplier;
                message = "Rapid Fire Activated For 10 seconds!";
                break;
            case PowerUpType.MultiShot:
                _isMultiShotActive = true;
                _multiShotTimer = MULTI_SHOT_DURATION;
                message = "Multi-Shot Activated For 5 seconds!";
                break;
        }

        // Add notification
        if (!string.IsNullOrEmpty(message))
        {
            _notifications.Add((message, NOTIFICATION_DURATION));
        }
    }

    private void UpdateNotifications(float deltaTime)
    {
        for (int i = _notifications.Count - 1; i >= 0; i--)
        {
            var notification = _notifications[i];
            notification.timer -= deltaTime;
            _notifications[i] = notification;

            if (notification.timer <= 0)
            {
                _notifications.RemoveAt(i);
            }
        }
    }

    private void UpdateLevel()
    {
        int newLevel = (_score / 1000) + 1;
        if (newLevel != _currentLevel)
        {
            _currentLevel = newLevel;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin();

        if (_currentState == GameState.Paused)
        {
            // PAUSE MENU
            DrawPauseMenu();
        }
        else if (!_isPlaying)
        {
            // MAIN MENU
            string title = "Starlight Reaver";
            string startText = "Press ENTER to Start";
            string pauseHint = "Press ESC to Exit";

            Vector2 titleSize = _gameFont.MeasureString(title);
            Vector2 startTextSize = _gameFont.MeasureString(startText);
            Vector2 pauseHintSize = _gameFont.MeasureString(pauseHint);

            Vector2 titlePos = new Vector2(
                (GameConfig.WINDOW_WIDTH - titleSize.X) / 2,
                GameConfig.WINDOW_HEIGHT / 3
            );

            Vector2 startTextPos = new Vector2(
                (GameConfig.WINDOW_WIDTH - startTextSize.X) / 2,
                titlePos.Y + titleSize.Y + 20
            );

            Vector2 pauseHintPos = new Vector2(
                (GameConfig.WINDOW_WIDTH - pauseHintSize.X) / 2,
                startTextPos.Y + startTextSize.Y + 10
            );

            _spriteBatch.DrawString(_gameFont, title, titlePos, Color.White);
            _spriteBatch.DrawString(_gameFont, startText, startTextPos, Color.Yellow);
            _spriteBatch.DrawString(_gameFont, pauseHint, pauseHintPos, Color.Yellow);
        }
        else
        {
            // GAMEPLAY
            // Draw Player
            _spriteBatch.Draw(_playerTexture,
                new Rectangle(_playerPosition.X, _playerPosition.Y, 64, 64),
                Color.White);

            // Draw enemies using their own draw method
            foreach (var enemy in _enemies)
                enemy.Draw(_spriteBatch);

            // Draw bullets
            foreach (var bullet in _playerBullets)
                _spriteBatch.Draw(_bulletTexture, bullet, Color.Yellow);

            foreach (var bullet in _enemyBullets)
                _spriteBatch.Draw(_bulletTexture, bullet, Color.Red);

            // Draw Multi-Shot and Rapid-Fire Bullets
            foreach (var bullet in _multiShotBullets)
                _spriteBatch.Draw(_bulletTexture, bullet, Color.Yellow);

            foreach (var bullet in _rapidFireBullets)
                _spriteBatch.Draw(_bulletTexture, bullet, Color.Orange);

            // Power-ups
            foreach (var powerUp in _powerUps)
                _spriteBatch.Draw(_bulletTexture, powerUp.Bounds, powerUp.Color);

            // Explosions
            foreach (var explosion in _explosions)
            {
                float scale = 1 + (0.3f - explosion.time);
                Color color = Color.White * (explosion.time / 0.3f);
                Rectangle pos = explosion.position;
                pos.Inflate((int)(16 * scale), (int)(16 * scale));
                _spriteBatch.Draw(_explosionTexture, pos, color);
            }

            // HUD
            int padding = 10;

            // Score
            _spriteBatch.DrawString(_gameFont, _score.ToString(),
                new Vector2(padding, padding), Color.Yellow);

            // Level
            string levelText = _currentLevel.ToString();
            Vector2 levelPos = new Vector2(
                GameConfig.WINDOW_WIDTH - _gameFont.MeasureString(levelText).X - padding,
                padding
            );
            _spriteBatch.DrawString(_gameFont, levelText, levelPos, Color.Green);

            // Power-up Status
            string powerStatus = $"DMG:x{_playerDamageMultiplier:F1} SPD:x{_playerSpeedMultiplier:F1}";
            Vector2 statusPos = new Vector2(10, 40);
            _spriteBatch.DrawString(_gameFont, powerStatus, statusPos, Color.White);

            // Lives
            for (int i = 0; i < _lives; i++)
            {
                _spriteBatch.Draw(_playerTexture,
                    new Rectangle(padding + i * 30, GameConfig.WINDOW_HEIGHT - 35, 25, 25),
                    Color.Red);
            }

            // Notifications
            for (int i = 0; i < _notifications.Count; i++)
            {
                var notification = _notifications[i];
                float alpha = Math.Min(notification.timer, 1f); // Fade out effect
                Vector2 pos = new Vector2(GameConfig.WINDOW_WIDTH / 2, 100 + (i * 30));
                Vector2 textSize = _gameFont.MeasureString(notification.message);
                Vector2 textPos = new Vector2(pos.X - (textSize.X / 2), pos.Y);

                _spriteBatch.DrawString(_gameFont, notification.message, textPos, Color.White * alpha);
            }
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawPauseMenu()
    {
        string title = "Pause Menu";
        Vector2 titlePosition = new Vector2(200, 50);
        _spriteBatch.DrawString(_gameFont, title, titlePosition, Color.Yellow);

        for (int i = 0; i < _pauseMenuOptions.Length; i++)
        {
            Color optionColor = (_selectedMenuOption == i) ? Color.Red : Color.White;
            string optionText = _pauseMenuOptions[i];

            if (_pauseMenuOptions[i] == "Music Volume")
                optionText += $": {_musicVolume:P0}";
            else if (_pauseMenuOptions[i] == "Shoot Volume")
                optionText += $": {_shootVolume:P0}";
            else if (_pauseMenuOptions[i] == "Explosion Volume")
                optionText += $": {_explosionVolume:P0}";

            Vector2 optionPosition = new Vector2(200, 100 + i * 40);
            _spriteBatch.DrawString(_gameFont, optionText, optionPosition, optionColor);
        }
    }
}
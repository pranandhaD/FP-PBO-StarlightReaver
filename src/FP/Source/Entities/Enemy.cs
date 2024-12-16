using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using FP.Source.Config;

namespace FP.Source.Entities;
public class Enemy
{
    // The texture used for rendering the enemy's sprite.
    private readonly Texture2D _texture;
    private Vector2 _position;
    private Rectangle _bounds;
    private int _shootCooldown;

    public Texture2D Texture => _texture;
    public Vector2 Position => _position;

    // Rectangle representing the enemy's bounding box (used for collisions).
    public Rectangle Bounds => new Rectangle((int)_position.X, (int)_position.Y, _texture.Width, _texture.Height);
    public int Width => _texture.Width;
    public int Height => _texture.Height;

    private readonly Random _random;
    public const int DROP_CHANCE = 30; // 30% chance to drop power-up

    // Indicates if the enemy can shoot (when cooldown reaches zero).
    public bool CanShoot => _shootCooldown <= 0;

    public Enemy(Texture2D texture, Vector2 position)
    {
        _texture = texture;
        _position = position;
        _shootCooldown = GameConfig.ENEMY_SHOOT_INTERVAL;
        _random = new Random();
    }

    public bool ShouldDropPowerUp()
    {
        return _random.Next(100) < DROP_CHANCE;
    }

    public PowerUpType GetRandomPowerUpType()
    {
        return (PowerUpType)_random.Next(Enum.GetValues(typeof(PowerUpType)).Length);
    }

    public void Update()
    {
        _position.Y += GameConfig.ENEMY_SPEED;
        if (_shootCooldown > 0)
            _shootCooldown--;
    }

    public void ResetShootCooldown()
    {
        _shootCooldown = GameConfig.ENEMY_SHOOT_INTERVAL;
    }

    // Draws the enemy's sprite on the screen using the SpriteBatch.
    public void Draw(SpriteBatch spriteBatch)
    {
        // Create a destination rectangle that forces the size we want
        Rectangle destinationRect = new Rectangle(
            (int)_position.X,
            (int)_position.Y,
            64,
            64
        );

        spriteBatch.Draw(_texture, destinationRect, Color.White);
    }
}
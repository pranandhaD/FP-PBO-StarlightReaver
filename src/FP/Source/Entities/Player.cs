using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FP.Source.Config;

namespace FP.Source.Entities;

public class Player
{
    // Texture used for rendering the player's sprite.
    private readonly Texture2D _texture;

    private Vector2 _position;

    private int _shootCooldown;

    // Indicates if the player can shoot (when cooldown reaches zero).
    public bool CanShoot => _shootCooldown <= 0;

    // Rectangle representing the player's bounding box (used for collisions).
    public Rectangle Bounds => new((int)_position.X, (int)_position.Y, _texture.Width, _texture.Height);

    public int Width => _texture.Width;

    public int Height => _texture.Height;

    public Player(Texture2D texture, Vector2 position)
    {
        _texture = texture;
        _position = position;
        _shootCooldown = 0;
    }

    // Updates the player's position and handles input for movement.
    public void Update()
    {
        var keyboard = Keyboard.GetState();

        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
            _position.X -= GameConfig.PLAYER_SPEED;

        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
            _position.X += GameConfig.PLAYER_SPEED;

        // Clamp the player's position to prevent moving outside the screen horizontally.
        _position.X = MathHelper.Clamp(_position.X, 0, GameConfig.WINDOW_WIDTH - Width);

        // Decrement the shoot cooldown timer.
        if (_shootCooldown > 0)
            _shootCooldown--;
    }

    public Vector2 GetBulletSpawnPosition()
    {
        // Bullets spawn from the center-top of the player's sprite.
        return new Vector2(_position.X + Width / 2 - 2, _position.Y);
    }

    // Resets the shoot cooldown to its initial value, defined in GameConfig.
    public void ResetShootCooldown()
    {
        _shootCooldown = GameConfig.PLAYER_SHOOT_COOLDOWN;
    }

    // Resets the player's position and shoot cooldown, typically used when starting or restarting the game.
    public void Reset()
    {
        _position = new Vector2(
            GameConfig.WINDOW_WIDTH / 2 - Width / 2, // Center the player horizontally.
            GameConfig.WINDOW_HEIGHT - Height - 20  // Position the player near the bottom of the screen.
        );
        _shootCooldown = 0; // Reset shooting cooldown.
    }
}

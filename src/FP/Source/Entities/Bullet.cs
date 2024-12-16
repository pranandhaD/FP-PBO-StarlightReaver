using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FP.Source.Entities;

public class Bullet
{
    // The texture used for rendering the bullet.
    private readonly Texture2D _texture;

    private Vector2 _position;

    private readonly float _speed;

    // Public property to access the bullet's position.
    public Vector2 Position => _position;

    // Public property to get the bounding box of the bullet (used for collision detection).
    public Rectangle Bounds => new((int)_position.X, (int)_position.Y, _texture.Width, _texture.Height);

    // Constructor to initialize the bullet's texture, starting position, and speed.
    public Bullet(Texture2D texture, Vector2 position, float speed)
    {
        _texture = texture;
        _position = position;
        _speed = speed;
    }

    // Updates the bullet's position by moving it vertically based on its speed.
    public void Update()
    {
        _position.Y += _speed; // Move the bullet up or down depending on speed (negative for up, positive for down).
    }
}

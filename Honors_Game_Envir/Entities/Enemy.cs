﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Survivor_of_the_Bulge
{
    public class Enemy
    {
        // Textures for movement.
        protected Texture2D backTexture, frontTexture, leftTexture;
        protected Texture2D bulletHorizontalTexture, bulletVerticalTexture;

        public Vector2 Position;
        public float MovementSpeed { get; set; } = 100f;
        public int Health { get; protected set; }
        public int BulletDamage { get; set; } = 5;
        public float FiringInterval { get; set; } = 2f;
        public float BulletRange { get; set; } = 1000f;
        public int CollisionDamage { get; set; } = 15;
        protected float timeSinceLastShot = 0f;
        protected float collisionDamageTimer = 0f;

        // Animation fields.
        protected Rectangle sourceRectangle;
        protected float frameTime = 0.1f;
        protected float timer = 0f;
        protected int currentFrame = 0;
        protected int totalFrames = 4;
        protected List<Bullet> bullets;
        protected float bulletSpeed = 300f;

        public enum EnemyState { Idle, Patrol, Chase, Attack, Dead }
        public enum Direction { Left, Right, Up, Down }
        protected EnemyState currentState;
        protected Direction currentDirection;
        protected const int CollisionPadding = 5;
        protected float shootingRange = 200f;
        protected float chaseRange = 400f;

        public int ExperienceReward { get; set; } = 10;
        private bool experienceAwarded = false;

        // Center-based bounding box.
        public virtual Rectangle Bounds
        {
            get
            {
                int frameW = sourceRectangle.Width;
                int frameH = sourceRectangle.Height;
                return new Rectangle((int)(Position.X - frameW / 2f), (int)(Position.Y - frameH / 2f), frameW, frameH);
            }
        }

        public bool IsDead => isDead;
        protected bool isDead = false;

        // Base stats stored on spawn.
        protected int baseHealth;
        protected int baseDamage;

        public Enemy(Texture2D back, Texture2D front, Texture2D left,
                     Texture2D bulletHorizontal, Texture2D bulletVertical,
                     Vector2 startPosition, Direction startDirection,
                     int health, int bulletDamage)
        {
            backTexture = back;
            frontTexture = front;
            leftTexture = left;
            bulletHorizontalTexture = bulletHorizontal;
            bulletVerticalTexture = bulletVertical;

            Position = startPosition;
            currentDirection = startDirection;
            Health = health;
            BulletDamage = bulletDamage;

            baseHealth = health;
            baseDamage = bulletDamage;

            MovementSpeed = 100f;
            bulletSpeed = 300f;
            frameTime = 0.1f;
            FiringInterval = 2f;
            timeSinceLastShot = 0f;
            collisionDamageTimer = 0f;
            currentFrame = 0;
            int frameW = leftTexture.Width / totalFrames;
            sourceRectangle = new Rectangle(0, 0, frameW, leftTexture.Height);
            bullets = new List<Bullet>();
            currentState = EnemyState.Patrol;
        }

        /// <summary>
        /// Store base health and damage values.
        /// </summary>
        public void SetBaseStats(int health, int damage)
        {
            baseHealth = health;
            baseDamage = damage;
        }

        /// <summary>
        /// Applies difficulty modifiers to MovementSpeed and BulletDamage.
        /// Also, if the current Health exceeds the new maximum (baseHealth × multiplier), reduce it.
        /// </summary>
        public virtual void ApplyDifficultyModifiers()
        {
            MovementSpeed = 100f * DifficultyManager.Instance.EnemySpeedMultiplier;
            BulletDamage = (int)(baseDamage * DifficultyManager.Instance.EnemyDamageMultiplier);

            int newMaxHealth = (int)(baseHealth * DifficultyManager.Instance.EnemyHealthMultiplier);
            if (Health > newMaxHealth)
                Health = newMaxHealth;
        }

        public virtual void Update(GameTime gameTime, Viewport viewport, Vector2 playerPosition, Player player)
        {
            if (isDead)
                return;

            // Update speed, damage, and adjust health if necessary.
            ApplyDifficultyModifiers();

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float distance = Vector2.Distance(Position, playerPosition);

            if (distance <= shootingRange)
                currentState = EnemyState.Attack;
            else if (distance <= chaseRange)
                currentState = EnemyState.Chase;
            else
                currentState = EnemyState.Patrol;

            switch (currentState)
            {
                case EnemyState.Patrol:
                    Patrol(viewport);
                    break;
                case EnemyState.Chase:
                    ChasePlayer(playerPosition);
                    break;
                case EnemyState.Attack:
                    timeSinceLastShot += delta;
                    if (timeSinceLastShot >= FiringInterval)
                    {
                        Shoot();
                        timeSinceLastShot = 0f;
                    }
                    break;
            }

            timer += delta;
            if (timer >= frameTime)
            {
                currentFrame = (currentFrame + 1) % totalFrames;
                timer = 0f;
            }
            UpdateFrameDimensions();

            foreach (var bullet in bullets)
            {
                bullet.Update(gameTime);
                // If the bullet collides with the player's bounds.
                if (bullet.IsActive && player.Bounds.Intersects(bullet.Bounds))
                {
                    player.TakeDamage(bullet.Damage);
                    bullet.Deactivate();
                    // Increment the counter for bullets used against enemies.
                    Game1.Instance.bulletsUsedAgainstEnemiesThisSession++;
                }
            }
            bullets.RemoveAll(b => !b.IsActive);
        }

        protected virtual void UpdateFrameDimensions()
        {
            int textureWidth = 0, textureHeight = 0;
            switch (currentDirection)
            {
                case Direction.Up:
                    textureWidth = backTexture.Width;
                    textureHeight = backTexture.Height;
                    break;
                case Direction.Down:
                    textureWidth = frontTexture.Width;
                    textureHeight = frontTexture.Height;
                    break;
                case Direction.Left:
                case Direction.Right:
                    textureWidth = leftTexture.Width;
                    textureHeight = leftTexture.Height;
                    break;
            }
            int frameW = textureWidth / totalFrames;
            int frameH = textureHeight;
            sourceRectangle = new Rectangle(currentFrame * frameW, 0, frameW, frameH);
        }

        public virtual void TakeDamage(int amount, Player player)
        {
            if (isDead)
                return;

            Health -= amount;
            if (Health <= 0)
            {
                isDead = true;
                currentState = EnemyState.Dead;
                Debug.WriteLine("Enemy died!");
                AwardExperience(player);
            }
        }

        protected void AwardExperience(Player player)
        {
            if (!experienceAwarded)
            {
                player.GainExperience(ExperienceReward);
                experienceAwarded = true;
            }
        }

        public virtual void Draw(SpriteBatch spriteBatch)
        {
            if (isDead)
                return;

            Texture2D currentTexture = frontTexture;
            SpriteEffects spriteEffects = SpriteEffects.None;
            switch (currentDirection)
            {
                case Direction.Left:
                    currentTexture = leftTexture;
                    break;
                case Direction.Right:
                    currentTexture = leftTexture;
                    spriteEffects = SpriteEffects.FlipHorizontally;
                    break;
                case Direction.Up:
                    currentTexture = backTexture;
                    break;
                case Direction.Down:
                    currentTexture = frontTexture;
                    break;
            }
            spriteBatch.Draw(currentTexture, Position, sourceRectangle, Color.White, 0f, Vector2.Zero, 1f, spriteEffects, 0f);

            foreach (var bullet in bullets)
            {
                bullet.Draw(spriteBatch);
            }
        }

        protected virtual void Patrol(Viewport viewport)
        {
            // Implement patrol behavior if desired.
        }

        protected virtual void ChasePlayer(Vector2 playerPosition)
        {
            Vector2 directionVector = playerPosition - Position;
            if (directionVector != Vector2.Zero)
            {
                Vector2 moveDir = Vector2.Normalize(directionVector);
                Position += moveDir * MovementSpeed * 0.02f;
                if (Math.Abs(moveDir.X) > Math.Abs(moveDir.Y))
                    currentDirection = moveDir.X < 0 ? Direction.Left : Direction.Right;
                else
                    currentDirection = moveDir.Y < 0 ? Direction.Up : Direction.Down;
            }
        }

        protected virtual void Shoot()
        {
            Vector2 bulletDirection;
            switch (currentDirection)
            {
                case Direction.Up:
                    bulletDirection = new Vector2(0, -1);
                    break;
                case Direction.Down:
                    bulletDirection = new Vector2(0, 1);
                    break;
                case Direction.Left:
                    bulletDirection = new Vector2(-1, 0);
                    break;
                case Direction.Right:
                    bulletDirection = new Vector2(1, 0);
                    break;
                default:
                    bulletDirection = new Vector2(1, 0);
                    break;
            }
            Vector2 bulletPos = Position;
            Bullet bullet = new Bullet(
                (currentDirection == Direction.Left || currentDirection == Direction.Right) ? bulletHorizontalTexture : bulletVerticalTexture,
                bulletPos,
                bulletDirection,
                bulletSpeed,
                BulletDamage,
                SpriteEffects.None,
                BulletRange
            );
            bullets.Add(bullet);
        }
    }
}

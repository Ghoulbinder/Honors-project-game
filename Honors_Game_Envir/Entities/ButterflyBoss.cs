﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace Survivor_of_the_Bulge
{
    public class ButterflyBoss : Boss
    {
        public enum ButterflyBossState { Walking, Attack, Death }
        public ButterflyBossState CurrentState { get; private set; } = ButterflyBossState.Walking;
        private float stateTimer = 0f;

        // Textures for the ButterflyBoss:
        // Attack texture: 1024x1280, 4 columns x 5 rows (20 frames)
        private Texture2D attackTexture;
        // Walking texture: 1024x1280, 4 columns x 5 rows (20 frames)
        private Texture2D walkingTexture;

        // Animation settings for Attack.
        private const int attackFramesPerRow = 4;
        private const int attackRows = 5;
        private const int attackTotalFrames = attackFramesPerRow * attackRows; // 20 frames
        private float attackFrameTime = 0.08f;

        // Animation settings for Walking.
        private const int walkingFramesPerRow = 4;
        private const int walkingRows = 5;
        private const int walkingTotalFrames = walkingFramesPerRow * walkingRows; // 20 frames
        private float walkingFrameTime = 0.08f;

        private float animTimer = 0f;
        private int frameIndex = 0;

        // Last known player position for aiming.
        private Vector2 lastTargetPosition;

        // New bullet textures for ButterflyBoss.
        private Texture2D butterflyBulletHorizontal;
        private Texture2D butterflyBulletVertical;

        /// <summary>
        /// Constructs a ButterflyBoss with no idle state.
        /// Parameters (8 total):
        /// attackTexture, walkingTexture,
        /// bulletHorizontal, bulletVertical,
        /// startPosition, startDirection, health, bulletDamage.
        /// </summary>
        public ButterflyBoss(
            Texture2D attackTexture,
            Texture2D walkingTexture,
            Texture2D bulletHorizontal,
            Texture2D bulletVertical,
            Vector2 startPosition,
            Direction startDirection,
            int health,
            int bulletDamage)
            : base(attackTexture, attackTexture, attackTexture, bulletHorizontal, bulletVertical, startPosition, startDirection, health, bulletDamage)
        {
            this.attackTexture = attackTexture;
            this.walkingTexture = walkingTexture;
            butterflyBulletHorizontal = bulletHorizontal;
            butterflyBulletVertical = bulletVertical;
            MovementSpeed = 120f;
            FiringInterval = 1.5f;
            BulletRange = 500f;
            CollisionDamage = 30;
            CurrentState = ButterflyBossState.Walking;
            animTimer = 0f;
            frameIndex = 0;
        }

        public override void Update(GameTime gameTime, Viewport viewport, Vector2 playerPosition, Player player)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            stateTimer += delta;
            lastTargetPosition = playerPosition;
            float distance = Vector2.Distance(Position, playerPosition);

            // FSM: if distance > 200, remain in Walking; otherwise, switch to Attack.
            if (distance > 200)
                CurrentState = ButterflyBossState.Walking;
            else
                CurrentState = ButterflyBossState.Attack;

            // Select animation parameters based on state.
            float frameTime;
            int totalFrames;
            int framesPerRowUsed;
            if (CurrentState == ButterflyBossState.Attack)
            {
                frameTime = attackFrameTime;
                totalFrames = attackTotalFrames;
                framesPerRowUsed = attackFramesPerRow;
            }
            else // Walking state.
            {
                frameTime = walkingFrameTime;
                totalFrames = walkingTotalFrames;
                framesPerRowUsed = walkingFramesPerRow;
            }
            animTimer += delta;
            if (animTimer >= frameTime)
            {
                frameIndex = (frameIndex + 1) % totalFrames;
                animTimer = 0f;
            }

            // Behavior.
            if (CurrentState == ButterflyBossState.Walking)
            {
                ChasePlayer(playerPosition);
            }
            else if (CurrentState == ButterflyBossState.Attack)
            {
                timeSinceLastShot += delta;
                if (timeSinceLastShot >= FiringInterval)
                {
                    Shoot();
                    timeSinceLastShot = 0f;
                }
            }

            // Update bullets.
            foreach (var bullet in bullets)
            {
                bullet.Update(gameTime);
                if (bullet.IsActive && player.Bounds.Intersects(bullet.Bounds))
                {
                    player.TakeDamage(bullet.Damage);
                    bullet.Deactivate();
                }
            }
            bullets.RemoveAll(b => !b.IsActive);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if (IsDead)
                return;

            Texture2D currentTexture;
            int framesPerRowUsed, rowsUsed;
            if (CurrentState == ButterflyBossState.Attack)
            {
                currentTexture = attackTexture;
                framesPerRowUsed = attackFramesPerRow;
                rowsUsed = attackRows;
            }
            else // Walking.
            {
                currentTexture = walkingTexture;
                framesPerRowUsed = walkingFramesPerRow;
                rowsUsed = walkingRows;
            }
            int frameW = currentTexture.Width / framesPerRowUsed;
            int frameH = currentTexture.Height / rowsUsed;
            Rectangle srcRect = new Rectangle((frameIndex % framesPerRowUsed) * frameW,
                                              (frameIndex / framesPerRowUsed) * frameH,
                                              frameW, frameH);
            Vector2 origin = new Vector2(frameW / 2f, frameH / 2f);
            spriteBatch.Draw(currentTexture, Position, srcRect, Color.White, 0f, origin, Scale, SpriteEffects.None, 0f);

            foreach (var bullet in bullets)
                bullet.Draw(spriteBatch);
        }

        public override Rectangle Bounds
        {
            get
            {
                int size = 77; // Fixed collision box size (same as player's) centered on the boss.
                return new Rectangle((int)(Position.X - size / 2), (int)(Position.Y - size / 2), size, size);
            }
        }

        protected override void Shoot()
        {
            Vector2 diff = lastTargetPosition - Position;
            if (diff != Vector2.Zero)
                diff.Normalize();
            else
                diff = new Vector2(1, 0);

            Texture2D chosenBulletTexture = (Math.Abs(diff.X) >= Math.Abs(diff.Y)) ? butterflyBulletHorizontal : butterflyBulletVertical;

            SpriteEffects effect = SpriteEffects.None;
            if (diff.X < 0)
                effect = SpriteEffects.FlipHorizontally;
            if (diff.Y > 0)
                effect = SpriteEffects.FlipVertically;

            Vector2 bulletPos = Position + diff * 20f;
            Bullet bullet = new Bullet(
                chosenBulletTexture,
                bulletPos,
                diff,
                500f,
                BulletDamage,
                effect,
                10000f // Large range so bullet only deactivates off-screen.
            );
            bullets.Add(bullet);
        }
    }
}

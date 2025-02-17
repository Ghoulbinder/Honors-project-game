﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace Survivor_of_the_Bulge
{
    public class ButterflyBoss : Boss
    {
        public enum ButterflyBossState { Idle, Walking, Attack, Death }
        public ButterflyBossState CurrentState { get; private set; } = ButterflyBossState.Idle;
        private float stateTimer = 0f;

        // Textures for the ButterflyBoss.
        // Idle: 1536x1024, 6 columns x 4 rows (24 frames)
        private Texture2D idleTexture;
        // Attack: (common) – using the same texture for attack.
        private Texture2D attackTexture;
        // Walking: (common) – using the same texture for walking.
        private Texture2D walkingTexture;

        // Animation settings for idle.
        private const int idleFramesPerRow = 6;
        private const int idleRows = 4;
        private const int idleTotalFrames = idleFramesPerRow * idleRows; // 24
        private float idleFrameTime = 0.15f;

        // Animation settings for attack/walking (common): assume same dimensions as idle.
        private float commonFrameTime = 0.08f;

        private float animTimer = 0f;
        private int frameIndex = 0;

        // Last seen player position for aiming.
        private Vector2 lastTargetPosition;

        /// <summary>
        /// ButterflyBoss constructor.
        /// Parameters (9 total):
        /// idleTexture, attackTexture, walkingTexture,
        /// bulletHorizontal, bulletVertical,
        /// startPosition, startDirection, health, bulletDamage.
        /// </summary>
        public ButterflyBoss(
            Texture2D idleTexture,
            Texture2D attackTexture,
            Texture2D walkingTexture,
            Texture2D bulletHorizontal,
            Texture2D bulletVertical,
            Vector2 startPosition,
            Direction startDirection,
            int health,
            int bulletDamage)
            : base(idleTexture, idleTexture, idleTexture, bulletHorizontal, bulletVertical, startPosition, startDirection, health, bulletDamage)
        {
            this.idleTexture = idleTexture;
            this.attackTexture = attackTexture;
            this.walkingTexture = walkingTexture;
            MovementSpeed = 120f;
            FiringInterval = 1.5f;
            BulletRange = 500f;
            CollisionDamage = 30;
            CurrentState = ButterflyBossState.Idle;
            animTimer = 0f;
            frameIndex = 0;
        }

        public override void Update(GameTime gameTime, Viewport viewport, Vector2 playerPosition, Player player)
        {
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            stateTimer += delta;
            lastTargetPosition = playerPosition;
            float distance = Vector2.Distance(Position, playerPosition);

            // Simple FSM: Idle if far, Walking if moderate, Attack if close.
            if (distance > 300)
                CurrentState = ButterflyBossState.Idle;
            else if (distance > 150)
                CurrentState = ButterflyBossState.Walking;
            else
                CurrentState = ButterflyBossState.Attack;

            // Use idle frame time for Idle, common for others.
            float frameTime = (CurrentState == ButterflyBossState.Idle) ? idleFrameTime : commonFrameTime;
            animTimer += delta;
            if (animTimer >= frameTime)
            {
                frameIndex = (frameIndex + 1) % idleTotalFrames;
                animTimer = 0f;
            }

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

            // Choose texture based on state.
            Texture2D currentTexture = (CurrentState == ButterflyBossState.Idle) ? idleTexture :
                                         (CurrentState == ButterflyBossState.Walking) ? walkingTexture :
                                         attackTexture;
            int frameW = currentTexture.Width / idleFramesPerRow;
            int frameH = currentTexture.Height / idleRows;
            Rectangle srcRect = new Rectangle((frameIndex % idleFramesPerRow) * frameW,
                                              (frameIndex / idleFramesPerRow) * frameH,
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
                // Fixed small collision box (same as player) centered on the boss.
                int size = 77;
                return new Rectangle((int)(Position.X - size / 2), (int)(Position.Y - size / 2), size, size);
            }
        }

        protected override void Shoot()
        {
            Vector2 direction = lastTargetPosition - Position;
            if (direction != Vector2.Zero)
                direction.Normalize();
            else
                direction = new Vector2(1, 0);
            Vector2 bulletPos = Position;
            bullets.Add(new Bullet(
                bulletHorizontalTexture,
                bulletPos,
                direction,
                500f,
                BulletDamage,
                SpriteEffects.None,
                BulletRange
            ));
        }
    }
}

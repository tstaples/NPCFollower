using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewValley.Monsters;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Summoning
{
    class SlimeMinion : BigSlime
    {
        private Monster CurrentTarget;
        private int Damage;

        public SlimeMinion(Vector2 position) : base(position)
        {
            Damage = this.damageToFarmer;
            this.damageToFarmer = 0;
            //this.speed = 7;
            this.c = Color.White;
            this.sprite = new AnimatedSprite(Game1.content.Load<Texture2D>("Characters\\Abigail"));
        }

        public override Rectangle GetBoundingBox()
        {
            if (this.sprite == null)
            {
                return Rectangle.Empty;
            }
            return new Rectangle((int)this.position.X + Game1.tileSize / 8, (int)this.position.Y + Game1.tileSize / 4, this.sprite.spriteWidth * Game1.pixelZoom * 3 / 4, Game1.tileSize / 2);
        }

        public override void update(GameTime time, GameLocation location)
        {
            base.update(time, location);
        }

        public override void updateMovement(GameLocation location, GameTime time)
        {
            if (this.timeBeforeAIMovementAgain > 0f)
                return;

            // Do default movement when there's no target.
            if (this.CurrentTarget == null)
                this.defaultMovementBehavior(time);
        }

        public override void behaviorAtGameTick(GameTime time)
        {
            if (this.timeBeforeAIMovementAgain > 0f)
                this.timeBeforeAIMovementAgain -= (float)time.ElapsedGameTime.Milliseconds;

            // Find a target if we don't have one or out current one is dead.
            if (this.CurrentTarget == null || this.CurrentTarget.health <= 0f)
            {
                this.CurrentTarget = GetClosestEnemy();
            }

            if (this.CurrentTarget != null)
            {
                MoveTowardsTarget(time);

                DamageIntersectingEnemies();
            }

            // Default slime tick behavior
            int currentFrame = this.sprite.CurrentFrame;
            this.sprite.AnimateDown(time, 0, "");
            this.sprite.interval = this.isMoving() ? 100f : 200f;
            if (Utility.isOnScreen(this.position, Game1.tileSize * 2) && this.sprite.CurrentFrame == 0 && currentFrame == 7)
            {
                Game1.playSound("slimeHit");
            }
        }

        private void MoveTowardsTarget(GameTime time)
        {
            var targetPos = this.CurrentTarget.GetBoundingBox().Center;
            var myPos = this.GetBoundingBox().Center;
            //if (Math.Abs(targetPos.Y - myPos.Y) < Game1.tileSize * 3)
            //{
            //    if (targetPos.X - myPos.X > 0)
            //    {
            //        this.SetMovingRight(true);
            //    }
            //    else
            //    {
            //        this.SetMovingLeft(true);
            //    }
            //}
            //else if (targetPos.Y - myPos.Y > 0)
            //{
            //    this.SetMovingDown(true);
            //}
            //else
            //{
            //    this.SetMovingUp(true);
            //}
            var direction = new Vector2(targetPos.X - myPos.X, myPos.Y - targetPos.Y);
            direction.Normalize();
            //var velocity = direction * (this.speed + this.addedSpeed);
            this.xVelocity = direction.X * this.speed;
            this.yVelocity = direction.Y * this.speed;
            this.MovePosition(time, Game1.viewport, Game1.currentLocation);
        }

        private void DamageIntersectingEnemies()
        {
            // Iter in reverse in case damaging them kills them causing the character array to be modified.
            for (int i = Game1.currentLocation.characters.Count - 1; i >= 0; --i)
            {
                var npc = Game1.currentLocation.characters[i];
                if (npc == this || !(npc is Monster) || npc is SlimeMinion)
                    continue;

                Monster enemy = npc as Monster;
                if (this.GetBoundingBox().Intersects(enemy.GetBoundingBox()))
                {
                    enemy.takeDamage(this.Damage, (int)this.xVelocity, (int)this.yVelocity, false, 0f);
                }
            }
        }

        private Monster GetClosestEnemy()
        {
            Monster closestNPC = null;
            float closestDistSq = float.MaxValue;
            float thresholdSq = this.moveTowardPlayerThreshold * this.moveTowardPlayerThreshold;
            foreach (var npc in Game1.currentLocation.characters)
            {
                if (!(npc is Monster) || npc == this || npc is SlimeMinion)
                    continue;

                var otherLocation = npc.getTileLocation();
                float distSq = (otherLocation - getTileLocation()).LengthSquared();
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestNPC = npc as Monster;
                }
            }

            return closestNPC;
        }

        public override void draw(SpriteBatch b)
        {
            Rectangle sourceRect = this.sprite.SourceRect;
            sourceRect.Height = sourceRect.Height + (int)((float)sourceRect.Height * 0.5f);
            //sourceRect.Y += this.sprite.spriteHeight / 2 + this.sprite.spriteHeight / 32;
            //sourceRect.Height = this.sprite.spriteHeight / 4;
            //sourceRect.X += this.sprite.spriteWidth / 4;
            //sourceRect.Width = this.sprite.spriteWidth / 2;
            b.Draw(this.Sprite.Texture, 
                   base.getLocalPosition(Game1.viewport) + new Vector2(Game1.tileSize * 3 / 4 + Game1.pixelZoom * 2, Game1.tileSize / 4 + this.yJumpOffset), 
                   sourceRect, 
                   this.c, 
                   this.rotation, 
                   new Vector2(16f, 16f), 
                   Math.Max(0.2f, this.scale) * (float)Game1.pixelZoom, 
                   this.flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                   Math.Max(0f, this.drawOnTop ? 0.991f : ((float)base.getStandingY() / 10000f)));
        }
    }
}

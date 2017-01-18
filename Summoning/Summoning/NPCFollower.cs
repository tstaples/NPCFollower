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
    class NPCFollower : NPC
    {
        /// <summary>Max distance an enemy can be to be considered.</summary>
        public float MaxAttackDistance { get; set; } = 1000f;

        /// <summary>How much damage we do to enemmies.</summary>
        public int Damage { get; set; } = 1;

        /// <summary>How often to deal damage in MS.</summary>
        public float DamageTick { get; set; } = 250f;

        /// <summary>How far enemies can be damaged from in pixels.</summary>
        public float DamageRadius { get; set; } = 100f;

        /// <summary>How long to wait in MS between path queries when one fails.</summary>
        public float MinPathRetryTime { get; set; } = 250f;

        /// <summary>Did the last search for a path fail.</summary>
        private bool FailedLastPathQuery => this.LastSearchedForPathTime.Ticks > 0;

        private Monster CurrentEnemy => this.CurrentTarget as Monster;
        private bool TargetIsEnemy => this.CurrentTarget is Monster;
        private bool TargetIsPlayer => this.CurrentTarget is Farmer;

        /// <summary>Our current target.</summary>
        private Character CurrentTarget;

        /// <summary>Are we currently following the player.</summary>
        //private bool FollowingPlayer = false;

        /// <summary>When the last search for a path failed.</summary>
        private TimeSpan LastSearchedForPathTime;

        /// <summary>The last time we dealt damage.</summary>
        private TimeSpan LastTimeDamageDealt;


        public NPCFollower(Vector2 position) 
            : base(new AnimatedSprite(Game1.content.Load<Texture2D>("Characters\\Abigail"), 0, Game1.tileSize / 4, Game1.tileSize * 2 / 4), 
                   position, 0, "Abigail")
        {
            this.willDestroyObjectsUnderfoot = false;
            this.followSchedule = false;
            this.collidesWithOtherCharacters = false;
            this.speed = Game1.player.speed;
            this.moveTowardPlayerThreshold = 3;
        }

        public override void update(GameTime time, GameLocation location)
        {
            base.update(time, location);

            if (!location.Equals(Game1.currentLocation))
                return;

            // Find a target if we don't have one or out current one is dead.
            if (this.CurrentEnemy == null || this.CurrentEnemy.health <= 0f)
            {
                // Choose the closest enemy or the player if there isn't one.
                this.CurrentTarget = GetClosestEnemy() ?? Game1.player as Character;
            }

            // Prevent our speed from being reset.
            this.speed = Game1.player.speed;

            if (this.CurrentTarget != null)
            {
                if (TooFarFromTarget())
                {
                    MoveTowardsTarget(time);
                }

                // Deal damage each damage tick.
                if (HasEnoughTimeElapsedMS(time, this.LastTimeDamageDealt, this.DamageTick))
                {
                    if (DamageIntersectingEnemies())
                    {
                        this.LastTimeDamageDealt = time.TotalGameTime;
                        Console.WriteLine("Dealt damage");
                    }
                }
            }
        }

        private void MoveTowardsTarget(GameTime time)
        {
            if (this.controller != null)
                return;

            // Check if enough time has elapsed since the last time a query failed.
            if (this.FailedLastPathQuery && !HasEnoughTimeElapsedMS(time, this.LastSearchedForPathTime, this.MinPathRetryTime))
                return;

            var targetLocation = this.CurrentTarget.getTileLocationPoint();
            var endPoint = this.TargetIsPlayer ? FindRandomPointAround(targetLocation) : targetLocation;
            this.controller = new PathFindController(this, Game1.currentLocation, endPoint, -1, OnPathComplete, int.MaxValue);
            if (this.controller?.pathToEndPoint != null)
            {
                Console.WriteLine($"Moving towards target: {this.CurrentTarget}");
                this.LastSearchedForPathTime = default(TimeSpan);
            }
            else
            {
                Console.WriteLine($"Failed to find path to target.");
                this.LastSearchedForPathTime = time.TotalGameTime;
            }
        }

        private void OnPathComplete(Character c, GameLocation location)
        {
            Console.WriteLine("Path complete");
            this.FollowingPlayer = false;
        }

        private bool TooFarFromTarget()
        {
            if (this.TargetIsPlayer && !withinPlayerThreshold())
                return true;

            var bb = GetBoundingBox();
            bb.Inflate((int)this.DamageRadius, (int)this.DamageRadius); // Expand by radius (or close to it).
            return !bb.Intersects(this.CurrentTarget.GetBoundingBox());
        }

        #region PathingUtil
        private Point FindRandomPointAround(Point p, int maxRadius = 3, int currentRadius = 0)
        {
            return FindRandomPointAround(new Vector2(p.X, p.Y), maxRadius, currentRadius);
        }

        private Point FindRandomPointAround(Vector2 p, int maxRadius = 3, int currentRadius = 0)
        {
            var surroundingTiles = Utility.getSurroundingTileLocationsArray(p);
            foreach (var tile in surroundingTiles)
            {
                if (Game1.currentLocation.isTilePassable(new xTile.Dimensions.Location((int)tile.X, (int)tile.Y), Game1.viewport))
                {
                    return new Point((int)tile.X, (int)tile.Y);
                }
                if (currentRadius < maxRadius)
                    return FindRandomPointAround(tile, maxRadius, currentRadius + 1);
            }
            return default(Point);
        }
        #endregion PathUtil

        #region Combat
        // Returns true if damage was dealt.
        private bool DamageIntersectingEnemies()
        {
            bool dealtDamage = false;

            var bb = GetBoundingBox();
            bb.Inflate((int)this.DamageRadius, (int)this.DamageRadius); // Expand by radius (or close to it).

            // Iter in reverse in case damaging them kills them causing the character array to be modified.
            for (int i = Game1.currentLocation.characters.Count - 1; i >= 0; --i)
            {
                var npc = Game1.currentLocation.characters[i];
                if (npc == this || !(npc is Monster) || npc is SlimeMinion)
                    continue;

                Monster enemy = npc as Monster;
                if (bb.Intersects(enemy.GetBoundingBox()))
                {
                    enemy.takeDamage(this.Damage, (int)this.xVelocity, (int)this.yVelocity, false, 0f);
                    dealtDamage = true;
                    //this.sprite.Animate(Game1.currentGameTime, 5, 2, 1000f);
                }
            }
            return dealtDamage;
        }

        private Monster GetClosestEnemy()
        {
            Monster closestNPC = null;
            float maxDistSq = this.MaxAttackDistance * this.MaxAttackDistance;
            float closestDistSq = maxDistSq + 1f;
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
        #endregion Combat

        private bool HasEnoughTimeElapsedMS(GameTime time, TimeSpan then, float amountMS)
        {
            return (time.TotalGameTime.Subtract(then).Milliseconds >= amountMS);
        }
    }
}

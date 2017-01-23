using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewValley.Monsters;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.Locations;

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

        /// <summary>Max number of tiles to iterate over when searching for a valid one.</summary>
        public int TileSearchLimit { get; set; } = 500;

        /// <summary>How many failed attempts in a row to allow before teleporting the npc to the player.</summary>
        public int SearchAttemptsBeforeTeleportingToPlayer { get; set; } = 10;

        /// <summary>Did the last search for a path fail.</summary>
        private bool FailedLastPathQuery => this.LastSearchedForPathTime.Ticks > 0;

        private Monster CurrentEnemy => this.CurrentTarget as Monster;
        private bool TargetIsEnemy => this.CurrentTarget is Monster;
        private bool TargetIsPlayer => this.CurrentTarget is Farmer;

        /// <summary>Our current target.</summary>
        private Character CurrentTarget;

        /// <summary>Number of times we've failed to find a path in a row.</summary>
        private int SequentialFailedPathAttempts;

        /// <summary>When the last search for a path failed.</summary>
        private TimeSpan LastSearchedForPathTime;

        /// <summary>The last time we dealt damage.</summary>
        private TimeSpan LastTimeDamageDealt;

        /// <summary>Tile type paired to weight.</summary>
        private Dictionary<string, int> TileTypePreferences;

        // Point, success
        private Dictionary<Point, bool> DebugPointsSearched;

        public NPCFollower(Vector2 position)
            //: base(new AnimatedSprite(Game1.content.Load<Texture2D>("Animals\\cat"), 0, 32, 32), position, 0, "cat")
            : base(new AnimatedSprite(Game1.content.Load<Texture2D>("Characters\\Abigail"), 0, Game1.tileSize / 4, Game1.tileSize * 2 / 4),
                   position, 0, "Abigail")
        {
            this.willDestroyObjectsUnderfoot = false;
            this.followSchedule = false;
            this.collidesWithOtherCharacters = false;
            this.speed = Game1.player.speed;
            this.moveTowardPlayerThreshold = 3;
            this.currentLocation = Game1.currentLocation;
            this.defaultMap = "FarmHouse";
            this.setMarried(true);

            // Values from PathFindController.getPreferenceValueForTerrainType
            this.TileTypePreferences = new Dictionary<string, int>()
            {
                { "stone", -7 },
                { "wood", -4 },
                { "dirt", -2 },
                { "grass", -1 }
            };

            this.DebugPointsSearched = new Dictionary<Point, bool>();

            StardewModdingAPI.Events.GraphicsEvents.OnPostRenderEvent += (sender, e) =>
            {
                if (this.DebugPointsSearched.Count > 0)
                {
                    Texture2D pixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
                    pixel.SetData(new[] { Color.White });

                    foreach (var pair in this.DebugPointsSearched)
                    {
                        Point p = new Point((pair.Key.X * Game1.tileSize) - Game1.viewport.X, (pair.Key.Y * Game1.tileSize) - Game1.viewport.Y);
                        Color color = pair.Value ? Color.Green : Color.Red;
                        Game1.spriteBatch.Draw(pixel, new Rectangle(p.X, p.Y, Game1.tileSize, Game1.tileSize), color * .3f);
                    }
                }
            };
        }

        public override bool checkAction(Farmer who, GameLocation l)
        {
            showTextAboveHead("Sup bitch.");
            return true;
        }

        public override bool shouldCollideWithBuildingLayer(GameLocation location)
        {
            return !(location is FarmHouse);
        }

        public override void update(GameTime time, GameLocation location)
        {
            base.update(time, location);

            if (!location.Equals(Game1.currentLocation))
            {
                Console.WriteLine($"Warping to path controller location: {Game1.currentLocation.Name}");
                Game1.warpCharacter(this, Game1.currentLocation.name, FindRandomPointAround(Game1.player.getTileLocationPoint()), false, this.currentLocation?.isOutdoors == true);
                this.controller = null; // reset
                if (Game1.currentLocation is FarmHouse)
                {
                    this.setTilePosition((this.currentLocation as FarmHouse).getEntryLocation());
                }
                //this.currentLocation = Game1.currentLocation;
                //warpToPathControllerDestination();
                return;
            }

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

        public override void draw(SpriteBatch b)
        {
            base.draw(b);
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

                this.SequentialFailedPathAttempts = this.FailedLastPathQuery ? this.SequentialFailedPathAttempts + 1 : 0;
                if (this.SequentialFailedPathAttempts >= this.SearchAttemptsBeforeTeleportingToPlayer)
                {
                    Console.WriteLine($"Max sequential fail attempts reached; teleporting to player.");
                    TeleportNearPlayer();

                    // Reset
                    this.SequentialFailedPathAttempts = 0;
                    this.LastSearchedForPathTime = default(TimeSpan);
                }
                else
                {
                    this.LastSearchedForPathTime = time.TotalGameTime;
                }
            }
        }

        private void OnPathComplete(Character c, GameLocation location)
        {
            Console.WriteLine("Path complete");
            //this.FollowingPlayer = false;
        }

        private bool TooFarFromTarget()
        {
            if (this.TargetIsPlayer)
                return !withinPlayerThreshold();

            var bb = GetBoundingBox();
            bb.Inflate((int)this.DamageRadius, (int)this.DamageRadius); // Expand by radius (or close to it).
            return !bb.Intersects(this.CurrentTarget.GetBoundingBox());
        }

        private void TeleportNearPlayer()
        {
            var teleportPoint = FindRandomPointAround(Game1.player.getTileLocation());
            setTilePosition(teleportPoint);
        }

        #region PathingUtil
        private Point FindRandomPointAround(Point p, float rad = 2f)
        {
            return FindRandomPointAround(new Vector2(p.X, p.Y), rad);
        }

        private Point FindRandomPointAround(Vector2 p, float rad = 2f)
        {
            this.DebugPointsSearched.Clear();

            var bb = GetBoundingBox();
            //var surroundingTiles = Utility.getSurroundingTileLocationsArray(p);
            var surroundingTiles = GetPointsAround(p, rad);
            foreach (var tile in surroundingTiles)
            {
                var point = new Point((int)tile.X, (int)tile.Y);
                var rect = new Rectangle((int)tile.X * Game1.tileSize, (int)tile.Y * Game1.tileSize, bb.Width, bb.Height);
                //if (Game1.currentLocation.isTilePassable(new xTile.Dimensions.Location((int)tile.X, (int)tile.Y), Game1.viewport))
                if (Game1.currentLocation.isCollidingPosition(rect, Game1.viewport, false, 0, false, null, false, false, true) ||
                    !IsPointReachable(this.getTileLocationPoint(), point))
                {
                    //this.DebugPointsSearched.Add(point, false);
                    continue;
                }

                //this.DebugPointsSearched.Add(point, true);
                return point;
            }
            return new Point((int)p.X, (int)p.Y);
        }

        private List<Vector2> GetPointsAround(Vector2 p, float r = 3f)
        {
            var points = new List<Vector2>();
            float rad = r;
            float radSquared = rad * rad;
            //for (float j = p.X - rad; j < p.X + rad; ++j)
            for (float j = p.X + rad; j > p.X - rad; --j)
            {
                //for (float k = p.Y - rad; k < p.Y + rad; ++k)
                for (float k = p.Y + rad; k > p.Y - rad; --k)
                {
                    var p2 = new Vector2(j, k);
                    float distToPointSq = Vector2.DistanceSquared(p, p2);
                    if (distToPointSq <= radSquared)
                    {
                        float distToMeSq = Vector2.DistanceSquared(this.getTileLocation(), p2);
                        int tileTypePreference = GetTilePreference(p2);

                        // Prefer points closer to the NPC slightly more than ones closer to the destination.
                        points.InsertAt(p2, otherPoint => 
                        {
                            float weight = 0f;
                            float otherToPointSq = Vector2.DistanceSquared(otherPoint, p);
                            float otherToMeSq = Vector2.DistanceSquared(otherPoint, this.getTileLocation());
                            weight += tileTypePreference < GetTilePreference(otherPoint) ? .2f : -.2f;
                            weight += distToPointSq < otherToPointSq ? 0.4f : -.4f;
                            weight += distToMeSq < otherToMeSq ? 0.6f : -.6f;
                            return weight > 0f;
                        });
                    }
                }
            }
            return points;
        }

        private bool IsPointReachable(Point start, Point end)
        {
            return (findPath(start, end, PathFindController.isAtEndPoint, this.currentLocation, this, this.TileSearchLimit) != null);
        }

        private int GetTilePreference(Vector2 tileLocation)
        {
            string tileType = this.currentLocation.doesTileHaveProperty((int)tileLocation.X, (int)tileLocation.Y, "Type", "Back");
            if (tileType != null && this.TileTypePreferences.ContainsKey(tileType.ToLower()))
            {
                return this.TileTypePreferences[tileType.ToLower()];
            }
            return 0;
        }

        public Stack<Point> findPath(Point startPoint, Point endPoint, PathFindController.isAtEnd endPointFunction, GameLocation location, Character character, int limit)
        {
            sbyte[,] array = new sbyte[,]
            {
                {
                    -1,
                    0
                },
                {
                    1,
                    0
                },
                {
                    0,
                    1
                },
                {
                    0,
                    -1
                }
            };
            PriorityQueue priorityQueue = new PriorityQueue();
            Dictionary<PathNode, PathNode> dictionary = new Dictionary<PathNode, PathNode>();
            int num = 0;
            priorityQueue.Enqueue(new PathNode(startPoint.X, startPoint.Y, 0, null), Math.Abs(endPoint.X - startPoint.X) + Math.Abs(endPoint.Y - startPoint.Y));
            while (!priorityQueue.IsEmpty())
            {
                PathNode pathNode = priorityQueue.Dequeue();
                if (endPointFunction(pathNode, endPoint, location, character))
                {
                    return PathFindController.reconstructPath(pathNode, dictionary);
                }
                if (!dictionary.ContainsKey(pathNode))
                {
                    dictionary.Add(pathNode, pathNode.parent);
                }
                for (int i = 0; i < 4; i++)
                {
                    PathNode pathNode2 = new PathNode(pathNode.x + (int)array[i, 0], pathNode.y + (int)array[i, 1], pathNode);
                    pathNode2.g = (byte)(pathNode.g + 1);
                    bool colliding = location.isCollidingPosition(new Rectangle(pathNode2.x * Game1.tileSize + 1, pathNode2.y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, false, 0, false, character, true, false, false);
                    //bool colliding = location.isTilePassable(new Rectangle(pathNode2.x * Game1.tileSize + 1, pathNode2.y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport);
                    //bool colliding = false;
                    if (!dictionary.ContainsKey(pathNode2) && 
                        ((pathNode2.x == endPoint.X && pathNode2.y == endPoint.Y) || (pathNode2.x >= 0 && pathNode2.y >= 0 && pathNode2.x < location.map.Layers[0].LayerWidth && pathNode2.y < location.map.Layers[0].LayerHeight)) &&
                        !colliding)
                    {
                        int priority = (int)pathNode2.g + (Math.Abs(endPoint.X - pathNode2.x) + Math.Abs(endPoint.Y - pathNode2.y));
                        if (!priorityQueue.Contains(pathNode2, priority))
                        {
                            if (!this.DebugPointsSearched.ContainsKey(new Point(pathNode2.x, pathNode2.y)))
                                this.DebugPointsSearched.Add(new Point(pathNode2.x, pathNode2.y), true);
                            priorityQueue.Enqueue(pathNode2, priority);
                        }
                    }
                    else if (!this.DebugPointsSearched.ContainsKey(new Point(pathNode2.x, pathNode2.y)))
                        this.DebugPointsSearched.Add(new Point(pathNode2.x, pathNode2.y), false);
                }
                num++;
                if (num >= limit)
                {
                    return null;
                }
            }
            return null;
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

using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Monsters;

namespace Summoning
{
    public class MyMod : Mod
    {
        public override void Entry(IModHelper helper)
        {
            ControlEvents.KeyPressed += ControlEvents_KeyPressed;
        }

        private void ControlEvents_KeyPressed(object sender, EventArgsKeyPressed e)
        {
            if (e.KeyPressed == Keys.G)
            {
                this.Monitor.Log("Summoning slimes", LogLevel.Trace);

                Farmer f = Game1.player;
                Farm farm = Game1.getFarm();
                var positions = new Microsoft.Xna.Framework.Vector2[]
                {
                    new Microsoft.Xna.Framework.Vector2(3489f, 1391f),
                    new Microsoft.Xna.Framework.Vector2(3489f, 1829f),
                    new Microsoft.Xna.Framework.Vector2(4270f, 1829f),
                    new Microsoft.Xna.Framework.Vector2(4270f, 1391f),
                };
                Random rnd = new Random();
                var pos = positions[rnd.Next(0, 4)];
                Monster monster = new GreenSlime(pos);
                monster.wildernessFarmMonster = true;
                Game1.currentLocation.characters.Add(monster);
            }
            else if (e.KeyPressed == Keys.H)
            {
                this.Monitor.Log("Summoning friendly slimes", LogLevel.Trace);

                Farmer f = Game1.player;
                Farm farm = Game1.getFarm();
                //NPCFollower follower = new NPCFollower(new Microsoft.Xna.Framework.Vector2(4200f, 1605f));
                NPCFollower follower = new NPCFollower(f.position);
                Game1.currentLocation.characters.Add(follower);
            }
        }
    }
}

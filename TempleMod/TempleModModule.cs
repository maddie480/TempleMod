using System;
using MonoMod.Cil;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Cecil;
using Microsoft.Xna.Framework;
using On.Celeste;
using Monocle;

namespace Celeste.Mod.TempleMod {
    public class TempleModModule : EverestModule {

        // this is what we will used to tag temple eyes with the "follow Madeline" option on
        // (I feel like it's not the intended usage at all, but have no better idea)
        private static BitTag followMadelineTag = new BitTag("followMadeline");

        public static TempleModModule Instance;

        public override Type SettingsType => null;

        public TempleModModule() {
            Instance = this;
        }

        // ================ Module loading ================

        public override void Load() {
            // mod methods here
            On.Celeste.TempleEye.ctor += ModTempleEyeConstructor;
            IL.Celeste.TempleEye.Update += ModTempleEyeUpdate;
        }

        public override void Unload() {
            // unmod methods here
            On.Celeste.TempleEye.ctor -= ModTempleEyeConstructor;
            IL.Celeste.TempleEye.Update -= ModTempleEyeUpdate;

            moddedMethods.Clear();
        }

        // ================ Utility methods for IL modding ================

        /// <summary>
        /// Keeps track of already patched methods.
        /// </summary>
        private static HashSet<string> moddedMethods = new HashSet<string>();

        /// <summary>
        /// Utility method to prevent methods from getting patched multiple times.
        /// </summary>
        /// <param name="methodName">Name of the patched method</param>
        /// <param name="patcher">Action to run in order to patch method</param>
        private static void ModMethod(string methodName, Action patcher) {
            // for whatever reason mod methods are called multiple times: only patch the methods once
            if (moddedMethods.Contains(methodName)) {
                Logger.Log("TempleModModule", $"> Method {methodName} already patched");
            } else {
                Logger.Log("TempleModModule", $"> Patching method {methodName}");
                patcher.Invoke();
                moddedMethods.Add(methodName);
            }
        }

        // ================ Temple eye handling ================

        /// <summary>
        /// Hooks to the TempleEye constructor.
        /// </summary>
        /// <param name="orig">The original constructor</param>
        /// <param name="self">The TempleEye being constructed</param>
        /// <param name="data">The entity settings</param>
        /// <param name="offset">(unused)</param>
        private void ModTempleEyeConstructor(On.Celeste.TempleEye.orig_ctor orig, TempleEye self, EntityData data, Vector2 offset) {
            orig(self, data, offset);

            if (data.Bool("followMadeline")) {
                // just store somehow in the entity that this flag was set (we cannot just add a field to the class...)
                self.AddTag(followMadelineTag);
            }
        }

        /// <summary>
        /// Patches the IL of the Update() method in TempleEye, to change what is tracked by the eye.
        /// </summary>
        /// <param name="il">Object allowing IL patching</param>
        private void ModTempleEyeUpdate(ILContext il) {
            ModMethod("TempleEyeUpdate", () => {
                ILCursor cursor = new ILCursor(il);

                if (cursor.TryGotoNext(MoveType.Before,
                    i => i.OpCode == OpCodes.Ldarg_0,
                    i => i.OpCode == OpCodes.Call && ((MethodReference)i.Operand).Name.Contains("get_Scene"),
                    i => i.OpCode == OpCodes.Callvirt && ((MethodReference)i.Operand).Name.Contains("get_Tracker"),
                    i => i.OpCode == OpCodes.Callvirt,
                    i => i.OpCode == OpCodes.Stloc_0)) {

                    Logger.Log("TempleModModule", $"Patching TempleEye at CIL index {cursor.Index} to be able to mod target");

                    // pop Theo and put a call to our method instead
                    cursor.RemoveRange(5);
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate<Func<TempleEye, Actor>>(ReturnTrackedActor);
                    cursor.Emit(OpCodes.Stloc_0);
                }
            });
        }

        /// <summary>
        /// Picks whether to track Madeline or Theo depending on the settings.
        /// </summary>
        /// <param name="self">The TempleEye object</param>
        /// <returns>Theo or Madeline depending on settings</returns>
        private Actor ReturnTrackedActor(TempleEye self) {
            if (self.TagCheck(followMadelineTag)) {
                return self.Scene.Tracker.GetEntity<Player>();
            }
            return self.Scene.Tracker.GetEntity<TheoCrystal>();
        }
    }
}

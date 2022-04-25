﻿//#define DEBUG_SPAM // enable this for incredible debug spam

using BaseX;
using CloudX.Shared;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace CloudHomeAccessLevel
{
    public class CloudHomeAccessLevel : NeosMod
    {
        public override string Name => "CloudHomeAccessLevel";
        public override string Author => "runtime";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/zkxs/NeosCloudHomeAccessLevel";

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_ENABLE = new ModConfigurationKey<bool>("enabled", "Enables the CloudHomeAccessLevel mod", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<SessionAccessLevel> KEY_DEFAULT_ACCESS_LEVEL = new ModConfigurationKey<SessionAccessLevel>("access_level", "Default access level for your cloud home", () => SessionAccessLevel.Private);

        private static ModConfiguration? config;
        private static MethodInfo? getConfiguredSessionAccessLevelMethod;
        private static MethodInfo? announceHomeOnLanGetter;
        private static ConstructorInfo? sessionAccessLevelConstructor;

        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(1, 0, 0))
                .AutoSave(false);
        }

        public override void OnEngineInit()
        {
            config = GetConfiguration();
            if (config == null)
            {
                Error("Could not load configuration");
                return;
            }

            // disable the mod if the enabled config has been set to false
            if (!config.GetValue(KEY_ENABLE)) // this is safe as the config has a default value
            {
                Debug("Mod disabled, returning early.");
                return;
            }

            Harmony harmony = new Harmony("dev.zkxs.neoscloudhomeaccesslevel");

            MethodInfo? openHomeOrCreate = AccessTools.DeclaredMethod(typeof(Userspace), "OpenHomeOrCreateTask", new Type[] { typeof(string), typeof(string), typeof(WorldOrb) });
            if (openHomeOrCreate == null)
            {
                Error("Could not find Userspace.OpenHomeOrCreateTask()");
                return;
            }

            announceHomeOnLanGetter = AccessTools.DeclaredPropertyGetter(typeof(Userspace), nameof(Userspace.AnnounceHomeOnLAN));
            if (announceHomeOnLanGetter == null)
            {
                Error("Could not find Userspace.AnnounceHomeOnLAN");
                return;
            }

            sessionAccessLevelConstructor = AccessTools.DeclaredConstructor(typeof(SessionAccessLevel?), new Type[] { typeof(SessionAccessLevel) });
            if (sessionAccessLevelConstructor == null)
            {
                Error("Could not find SessionAccessLevel? constructor");
                return;
            }

            MethodInfo openHomeOrCreateAsyncBody = GetAsyncMethodBody(openHomeOrCreate);
            MethodInfo transpiler = AccessTools.DeclaredMethod(typeof(CloudHomeAccessLevel), nameof(Transpiler));
            getConfiguredSessionAccessLevelMethod = AccessTools.DeclaredMethod(typeof(CloudHomeAccessLevel), nameof(GetConfiguredSessionAccessLevel));
            harmony.Patch(openHomeOrCreateAsyncBody, transpiler: new HarmonyMethod(transpiler));
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new(instructions);


            /* We are looking for the following:
             * 
             * IL_0172: call         bool FrooxEngine.Userspace::get_AnnounceHomeOnLAN()
             * IL_0177: brtrue.s     IL_017c
             * IL_0179: ldc.i4.0
             * IL_017a: br.s         IL_017d
             * IL_017c: ldc.i4.1
             * IL_017d: newobj       instance void valuetype [mscorlib]System.Nullable`1<valuetype [CloudX.Shared]CloudX.Shared.SessionAccessLevel>::.ctor(!0)
             * 
             * That code is checking if Userspace.AnnounceHomeOnLAN() is true, and if so it's loading SessionAccessLevel.LAN onto the stack.
             * Otherwise, it's loading SessionAccessLevel.Private onto the stack.
             */

            for (int i = 0; i < codes.Count - 5; i++)
            {
                if (codes[i + 0].Calls(announceHomeOnLanGetter) &&
                    codes[i + 1].opcode.Equals(OpCodes.Brtrue_S) &&
                    codes[i + 2].LoadsConstant((int)SessionAccessLevel.Private) &&
                    codes[i + 3].opcode.Equals(OpCodes.Br_S) &&
                    codes[i + 4].LoadsConstant((int)SessionAccessLevel.LAN) &&
                    codes[i + 5].opcode == OpCodes.Newobj && sessionAccessLevelConstructor!.Equals(codes[i + 5].operand)
                )
                {
                    // change the call to point to my GetConfiguredSessionAccessLevel() method
                    codes[i + 0].operand = getConfiguredSessionAccessLevelMethod;

                    // nuke the next four instructions
                    codes.RemoveAt(i + 1); // brtrue.s IL_017c
                    codes.RemoveAt(i + 1); // ldc.i4.0
                    codes.RemoveAt(i + 1); // br.s IL_017d
                    codes.RemoveAt(i + 1); // ldc.i4.1

#if DEBUG_SPAM
                    DebugCodes(codes);
#endif
                    return codes;
                }
            }

            throw new TranspilerException("Could not find expected instructions to patch");
        }

#if DEBUG_SPAM
        private static void DebugCodes(IEnumerable<CodeInstruction> instructions)
        {
            int index = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                Debug($"{index}: {instruction}");
            }
        }
#endif

        private static SessionAccessLevel GetConfiguredSessionAccessLevel()
        {
            SessionAccessLevel accessLevel = config!.GetValue(KEY_DEFAULT_ACCESS_LEVEL);
            Debug($"Forcing cloud home access level to {accessLevel}");
            return accessLevel;
        }

        private static MethodInfo GetAsyncMethodBody(MethodInfo asyncMethod)
        {
            AsyncStateMachineAttribute asyncAttribute = (AsyncStateMachineAttribute)asyncMethod.GetCustomAttribute(typeof(AsyncStateMachineAttribute));
            if (asyncAttribute == null)
            {
                throw new ReflectionException($"Could not find AsyncStateMachine for {asyncMethod}");
            }
            Type asyncStateMachineType = asyncAttribute.StateMachineType;
            MethodInfo asyncMethodBody = AccessTools.DeclaredMethod(asyncStateMachineType, "MoveNext");
            if (asyncMethodBody == null)
            {
                throw new ReflectionException($"Could not find async method body for {asyncMethod}");
            }
            return asyncMethodBody;
        }

        private class ReflectionException : Exception
        {
            public ReflectionException(string message) : base(message) { }
        }

        private class TranspilerException : Exception
        {
            public TranspilerException(string message) : base(message) { }
        }
    }
}

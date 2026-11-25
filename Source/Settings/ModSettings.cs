using System;
using System.IO;
using System.Xml;
using UnityEngine;
using Verse;

namespace HisaCat.EscClosesLetters.Settings
{
    public class LetterDismissModSettings : ModSettings
    {
        public ConfigValue<bool> useCancelKeyToDismiss = new(true);
        public bool UseCancelKeyToDismiss => useCancelKeyToDismiss.value;
        public ConfigValue<bool> useAcceptKeyToDismiss = new(true);
        public bool UseAcceptKeyToDismiss => useAcceptKeyToDismiss.value;
        public ConfigValue<bool> closeOnClickedOutside = new(true);
        public bool CloseOnClickedOutside => closeOnClickedOutside.value;

        public ConfigValue<bool> useOkOptionForDismiss = new(true);
        public bool UseOkOptionForDismiss => useOkOptionForDismiss.value;
        public ConfigValue<bool> useCloseOptionForDismiss = new(true);
        public bool UseCloseOptionForDismiss => useCloseOptionForDismiss.value;
        public ConfigValue<bool> usePostponeLetterOptionForDismiss = new(false);
        public bool UsePostponeLetterOptionForDismiss => usePostponeLetterOptionForDismiss.value;
        public ConfigValue<bool> useRejectOptionForDismiss = new(false);
        public bool UseRejectOptionForDismiss => useRejectOptionForDismiss.value;
        public ConfigValue<bool> enableLog = new(false);
        public bool EnableLog => enableLog.value;

        public class ConfigValue<T>
        {
            public T value;
            public readonly T defaultValue;
            public ConfigValue(T defaultValue)
            {
                this.value = defaultValue;
                this.defaultValue = defaultValue;
            }

            public void ResetToDefault() => this.value = this.defaultValue;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref this.useCancelKeyToDismiss.value, nameof(this.useCancelKeyToDismiss), this.useCancelKeyToDismiss.defaultValue);
            Scribe_Values.Look(ref this.useAcceptKeyToDismiss.value, nameof(this.useAcceptKeyToDismiss), this.useAcceptKeyToDismiss.defaultValue);
            Scribe_Values.Look(ref this.closeOnClickedOutside.value, nameof(this.closeOnClickedOutside), this.closeOnClickedOutside.defaultValue);

            Scribe_Values.Look(ref this.useOkOptionForDismiss.value, nameof(this.useOkOptionForDismiss), this.useOkOptionForDismiss.defaultValue);
            Scribe_Values.Look(ref this.useCloseOptionForDismiss.value, nameof(this.useCloseOptionForDismiss), this.useCloseOptionForDismiss.defaultValue);
            Scribe_Values.Look(ref this.usePostponeLetterOptionForDismiss.value, nameof(this.usePostponeLetterOptionForDismiss), this.usePostponeLetterOptionForDismiss.defaultValue);
            Scribe_Values.Look(ref this.useRejectOptionForDismiss.value, nameof(this.useRejectOptionForDismiss), this.useRejectOptionForDismiss.defaultValue);

            Scribe_Values.Look(ref this.enableLog.value, nameof(this.enableLog), this.enableLog.defaultValue);
            base.ExposeData();
        }

        public void ResetToDefaults()
        {
            this.useCancelKeyToDismiss.ResetToDefault();
            this.useAcceptKeyToDismiss.ResetToDefault();
            this.closeOnClickedOutside.ResetToDefault();

            this.useOkOptionForDismiss.ResetToDefault();
            this.useCloseOptionForDismiss.ResetToDefault();
            this.usePostponeLetterOptionForDismiss.ResetToDefault();
            this.useRejectOptionForDismiss.ResetToDefault();

            this.enableLog.ResetToDefault();
        }

        private static LetterDismissModSettings _instance = null;
        public static LetterDismissModSettings Instance
        {
            get => _instance ??= LoadedModManager.GetMod<LetterDismissMod>().GetSettings<LetterDismissModSettings>();
        }
    }

    public class LetterDismissMod : Mod
    {
        // LetterDismissModSettings lived in HisaCat.EscClosesLetters before the settings namespace move.
        // ScribeExtractor.SaveableFromNode calls Log.Error when this Class cannot be resolved, which opens
        // the log window, then falls back to the requested type. Rewrite the attribute before GetSettings
        // so the saved values load as the current type and that error is never raised.
        private const string LegacySettingsClassName = "HisaCat.EscClosesLetters.LetterDismissModSettings";

        private readonly LetterDismissModSettings settings = null;
        public LetterDismissMod(ModContentPack content) : base(content)
        {
            this.RewriteLegacySettingsClassAttribute();
            this.settings = this.GetSettings<LetterDismissModSettings>();
        }

        /// <summary>
        /// If the saved ModSettings Class is <see cref="LegacySettingsClassName"/>, replace only that attribute
        /// with <see cref="LetterDismissModSettings"/>'s current full name.
        /// </summary>
        /// <remarks>
        /// Mod.GetSettings loads LoadedModManager.ReadModSettings(Content.FolderName, mod type name).
        /// The filename is private GetSettingsFilename: ConfigFolderPath plus
        /// GenText.SanitizeFilename("Mod_{folder}_{modType}.xml"). Same expression is used here.
        /// Only the Class attribute value is changed, so existing setting values stay as saved.
        /// A missing file, the current class name, or any other class name is left untouched.
        /// </remarks>
        private void RewriteLegacySettingsClassAttribute()
        {
            string settingsPath = null;
            string temporaryPath = null;
            try
            {
                settingsPath = Path.Combine(
                    GenFilePaths.ConfigFolderPath,
                    GenText.SanitizeFilename($"Mod_{this.Content.FolderName}_{this.GetType().Name}.xml"));
                if (File.Exists(settingsPath) == false)
                    return;

                XmlDocument document = new();
                document.PreserveWhitespace = true;
                document.Load(settingsPath);

                XmlAttribute classAttribute = document.DocumentElement?["ModSettings"]?.Attributes?["Class"];
                if (classAttribute == null)
                    return;
                if (classAttribute.Value != LegacySettingsClassName)
                    return;

                classAttribute.Value = typeof(LetterDismissModSettings).FullName;
                // Save beside the original, then replace it. A failed Save must not truncate the settings file.
                temporaryPath = settingsPath + ".tmp";
                document.Save(temporaryPath);
                File.Replace(temporaryPath, settingsPath, null);
            }
            catch (Exception ex)
            {
                if (temporaryPath != null && File.Exists(temporaryPath))
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch (Exception deleteException)
                    {
                        Log.Warning($"[EscClosesLetters] Failed to delete temporary settings file '{temporaryPath}': {deleteException}");
                    }
                }

                Log.Warning($"[EscClosesLetters] Failed to rewrite the legacy settings class in '{settingsPath}': {ex}");
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new();
            listingStandard.Begin(inRect);
            {
                listingStandard.Label(KeyedKeys.LetterDismiss.Settings.UseKeysToDismiss.Translate());
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.UseCancelKeyToDismiss.Translate(RimWorld.KeyBindingDefOf.Cancel.MainKeyLabel), ref this.settings.useCancelKeyToDismiss.value);
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.UseAcceptKeyToDismiss.Translate(RimWorld.KeyBindingDefOf.Accept.MainKeyLabel), ref this.settings.useAcceptKeyToDismiss.value);
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.CloseOnClickedOutside.Translate(), ref this.settings.closeOnClickedOutside.value);
                listingStandard.Gap();

                listingStandard.Label(KeyedKeys.LetterDismiss.Settings.UseOptionsToDismiss.Translate());
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.UseOkOptionForDismiss.Translate("OK".Translate()), ref this.settings.useOkOptionForDismiss.value);
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.UseCloseOptionForDismiss.Translate("Close".Translate()), ref this.settings.useCloseOptionForDismiss.value);
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.UsePostponeLetterOptionForDismiss.Translate("PostponeLetter".Translate()), ref this.settings.usePostponeLetterOptionForDismiss.value);
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.UseRejectOptionForDismiss.Translate("RejectLetter".Translate()), ref this.settings.useRejectOptionForDismiss.value);
                listingStandard.Gap();

                listingStandard.Label(KeyedKeys.LetterDismiss.Settings.Debug.Translate());
                listingStandard.CheckboxLabeled(KeyedKeys.LetterDismiss.Settings.EnableLog.Translate(), ref this.settings.enableLog.value);
                listingStandard.Gap();

                if (listingStandard.ButtonText(KeyedKeys.LetterDismiss.Settings.ResetToDefaults.Translate()))
                {
                    this.settings.ResetToDefaults();
                }
                listingStandard.Gap();
            }
            listingStandard.End();

            // if (dirty) this.WriteSettings();

            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
            => KeyedKeys.LetterDismiss.EscClosesLetters.Translate();
    }
}

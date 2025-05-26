using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using System;
using System.Collections.Generic;

namespace UrbanInequality
{
    [FileLocation($"ModsSettings\\{nameof(UrbanInequality)}\\{nameof(UrbanInequality)}")]
    [SettingsUIGroupOrder(LevelCapGroup, EducationGroup, IncomeGroup)]
    [SettingsUIShowGroupName(LevelCapGroup, EducationGroup, IncomeGroup)]
    public class Setting : ModSetting
    {
        public const string IncomeSection = "IncomeSection";
        public const string EducationSection = "EducationSection";
        public const string LevelCapSection = "LevelCapSection";
        public const string IncomeGroup = "IncomeGroup";
        public const string EducationGroup = "EducationGroup";
        public const string LevelCapGroup = "LevelCapGroup";
        public Setting(IMod mod) : base(mod)
        {

            SetDefaults();
        }

        public override void SetDefaults()
        {
            minIncomePenalty = 1.1f;
            minEducationPenalty = 1.1f;
            maxIncomePenalty = 1.5f;
            maxEducationPenalty = 1.5f;
            selectedCity = CityOption.NewYork;
            ApplyCityCapPreset(selectedCity);
        }

        [SettingsUISlider(min = 0.1f, max = 1.5f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(IncomeSection, IncomeGroup)]
        public float minIncomePenalty { get; set; }

        [SettingsUISlider(min = 0.1f, max = 1.5f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(EducationSection, EducationGroup)]
        public float minEducationPenalty { get; set; }

        [SettingsUISlider(min = 0.1f, max = 1.5f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(IncomeSection, IncomeGroup)]
        public float maxIncomePenalty { get; set; }

        [SettingsUISlider(min = 0.1f, max = 1.5f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(EducationSection, EducationGroup)]
        public float maxEducationPenalty { get; set; }

        [SettingsUISection(LevelCapSection, LevelCapGroup)]
        public CityOption selectedCity { get; set; }

        [SettingsUIButton]
        [SettingsUISection(LevelCapSection, LevelCapGroup)]
        public bool ApplyPresetButton
        {
            set => ApplyCityCapPreset(selectedCity);
        }

        public float[] levelCaps = new float[6]; // index 0 unused

        private void ApplyCityCapPreset(CityOption city)
        {
            switch (city)
            {
                case CityOption.Berlin: levelCaps = new float[] { 0, 0.15f, 0.25f, 0.35f, 0.2f, 0.05f }; break;
                case CityOption.SaoPaulo: levelCaps = new float[] { 0, 0.4f, 0.3f, 0.2f, 0.08f, 0.02f }; break;
                case CityOption.Tokyo: levelCaps = new float[] { 0, 0.1f, 0.2f, 0.4f, 0.2f, 0.1f }; break;
                case CityOption.London: levelCaps = new float[] { 0, 0.1f, 0.2f, 0.3f, 0.25f, 0.15f }; break;
                case CityOption.Cairo: levelCaps = new float[] { 0, 0.5f, 0.3f, 0.15f, 0.04f, 0.01f }; break;
                case CityOption.NewYork: levelCaps = new float[] { 0, 0.05f, 0.15f, 0.25f, 0.3f, 0.25f }; break;
                case CityOption.Mumbai: levelCaps = new float[] { 0, 0.45f, 0.3f, 0.15f, 0.08f, 0.02f }; break;
                case CityOption.Paris: levelCaps = new float[] { 0, 0.1f, 0.2f, 0.3f, 0.25f, 0.15f }; break;
                case CityOption.LosAngeles: levelCaps = new float[] { 0, 0.05f, 0.15f, 0.3f, 0.3f, 0.2f }; break;
                case CityOption.Moscow: levelCaps = new float[] { 0, 0.2f, 0.3f, 0.3f, 0.15f, 0.05f }; break;
                case CityOption.Seoul: levelCaps = new float[] { 0, 0.08f, 0.2f, 0.35f, 0.25f, 0.12f }; break;
                case CityOption.Istanbul: levelCaps = new float[] { 0, 0.25f, 0.3f, 0.25f, 0.15f, 0.05f }; break;
                case CityOption.Bangkok: levelCaps = new float[] { 0, 0.35f, 0.3f, 0.2f, 0.1f, 0.05f }; break;
                case CityOption.Sydney: levelCaps = new float[] { 0, 0.05f, 0.15f, 0.35f, 0.3f, 0.15f }; break;
                case CityOption.MexicoCity: levelCaps = new float[] { 0, 0.3f, 0.3f, 0.25f, 0.1f, 0.05f }; break;
                case CityOption.Toronto: levelCaps = new float[] { 0, 0.1f, 0.25f, 0.3f, 0.25f, 0.1f }; break;
                case CityOption.BuenosAires: levelCaps = new float[] { 0, 0.3f, 0.3f, 0.25f, 0.1f, 0.05f }; break;
                case CityOption.Beijing: levelCaps = new float[] { 0, 0.25f, 0.3f, 0.3f, 0.1f, 0.05f }; break;
                case CityOption.CapeTown: levelCaps = new float[] { 0, 0.4f, 0.3f, 0.2f, 0.08f, 0.02f }; break;
                case CityOption.RioDeJaneiro: levelCaps = new float[] { 0, 0.35f, 0.3f, 0.2f, 0.1f, 0.05f }; break;
                default: levelCaps = new float[] { 0, 0.3f, 0.3f, 0.2f, 0.15f, 0.05f }; break;
            }
        }

        public enum CityOption
        {
            Berlin, SaoPaulo, Tokyo, London, Cairo,
            NewYork, Mumbai, Paris, LosAngeles, Moscow,
            Seoul, Istanbul, Bangkok, Sydney, MexicoCity,
            Toronto, BuenosAires, Beijing, CapeTown, RioDeJaneiro
        }

        public class LocaleEN : IDictionarySource
        {
            private readonly Setting m_Setting;
            public LocaleEN(Setting setting)
            {
                m_Setting = setting;
            }
            public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
            {
                return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Urban Inequality" },
                { m_Setting.GetOptionTabLocaleID(Setting.IncomeSection), "Income" },
                { m_Setting.GetOptionTabLocaleID(Setting.EducationSection), "Education" },
                { m_Setting.GetOptionTabLocaleID(Setting.LevelCapSection), "Level Cap" },
                { m_Setting.GetOptionGroupLocaleID(Setting.IncomeGroup), "Income" },
                { m_Setting.GetOptionGroupLocaleID(Setting.EducationGroup), "Education" },
                { m_Setting.GetOptionGroupLocaleID(Setting.LevelCapGroup), "Level Cap" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.minIncomePenalty)), "Min. Income Penalty" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.minIncomePenalty)), "Building level up penalty applied to residents in the highest income group" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.maxIncomePenalty)), "Max. Income Penalty" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.maxIncomePenalty)), "Building level up penalty applied to residents in the lowest income group" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.minEducationPenalty)), "Min. Education Penalty" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.minEducationPenalty)), "Building level up penalty applied to residents in the highest income group" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.maxEducationPenalty)), "Max. Education Penalty" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.maxEducationPenalty)), "Building level up penalty applied to residents in the lowest income group" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.selectedCity)), "City Level Caps" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.selectedCity)), "Limits the number of buildings that can exist in each building level based on a similar distribution from real world cities. Cities in richer and more developed countries will have higher limits for the higher building levels, while cities in less developed countries will have more buildings in the lower levels." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ApplyPresetButton)), "Apply Level Caps" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ApplyPresetButton)), "Apply new settings" },

                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Berlin), "Berlin" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.SaoPaulo), "São Paulo" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Tokyo), "Tokyo" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.London), "London" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Cairo), "Cairo" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.NewYork), "New York City" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Mumbai), "Mumbai" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Paris), "Paris" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.LosAngeles), "Los Angeles" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Moscow), "Moscow" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Seoul), "Seoul" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Istanbul), "Istanbul" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Bangkok), "Bangkok" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Sydney), "Sydney" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.MexicoCity), "Mexico City" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Toronto), "Toronto" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.BuenosAires), "Buenos Aires" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.Beijing), "Beijing" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.CapeTown), "Cape Town" },
                { m_Setting.GetEnumValueLocaleID(Setting.CityOption.RioDeJaneiro), "Rio de Janeiro" }

            };
            }

            public void Unload()
            {

            }

        }
    }
}

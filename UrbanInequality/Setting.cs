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
    [SettingsUIGroupOrder(CityGroup, LevelCapGroup, WageGroup, EducationGroup, IncomeGroup, OtherGroup)]
    [SettingsUIShowGroupName(CityGroup, LevelCapGroup, WageGroup, EducationGroup, IncomeGroup)]
    public class Setting : ModSetting
    {
        public const string IncomeSection = "IncomeSection";
        public const string EducationSection = "EducationSection";
        public const string LevelCapSection = "LevelCapSection";
        public const string WageSection = "WageSection";
        public const string CitySection = "CitySection";
        public const string OtherSection = "OtherSection";
        public const string IncomeGroup = "IncomeGroup";
        public const string EducationGroup = "EducationGroup";
        public const string LevelCapGroup = "LevelCapGroup";
        public const string WageGroup = "WageGroup";
        public const string CityGroup = "CityGroup";
        public const string OtherGroup = "OtherGroup";
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
            levelUpMaterialFactor = 1.5f;
            ApplyCityCapPreset(selectedCity);
            ApplyCityWagePreset(selectedCity);
        }

        [SettingsUISection(CitySection, CityGroup)]
        public CityOption selectedCity { get; set; }

        [SettingsUIButton]
        [SettingsUISection(CitySection, CityGroup)]
        public bool ApplyPresetButton
        {
            set
            {
                ApplyCityCapPreset(selectedCity);
                ApplyCityWagePreset(selectedCity);
            }
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

        public float[] levelCaps = new float[6]; // index 0 unused

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(LevelCapSection, LevelCapGroup)]
        public float levelCap1 { get => levelCaps[1]; set => levelCaps[1] = value; }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(LevelCapSection, LevelCapGroup)]
        public float levelCap2 { get => levelCaps[2]; set => levelCaps[2] = value; }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(LevelCapSection, LevelCapGroup)]
        public float levelCap3 { get => levelCaps[3]; set => levelCaps[3] = value; }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(LevelCapSection, LevelCapGroup)]
        public float levelCap4 { get => levelCaps[4]; set => levelCaps[4] = value; }

        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(LevelCapSection, LevelCapGroup)]
        public float levelCap5 { get => levelCaps[5]; set => levelCaps[5] = value; }

        // ---- Wage Sliders ----

        private int[] _wageLevels = new int[6];
        public int[] wageLevels => _wageLevels;

        [SettingsUISlider(min = 1000f, max = 8000f, step = 100f)]
        [SettingsUISection(WageSection, WageGroup)]
        public int wageLevel1 { get => _wageLevels[1]; set => _wageLevels[1] = value; }

        [SettingsUISlider(min = 1000f, max = 8000f, step = 100f)]
        [SettingsUISection(WageSection, WageGroup)]
        public int wageLevel2 { get => _wageLevels[2]; set => _wageLevels[2] = value; }

        [SettingsUISlider(min = 1000f, max = 8000f, step = 100f)]
        [SettingsUISection(WageSection, WageGroup)]
        public int wageLevel3 { get => _wageLevels[3]; set => _wageLevels[3] = value; }

        [SettingsUISlider(min = 1000f, max = 8000f, step = 100f)]
        [SettingsUISection(WageSection, WageGroup)]
        public int wageLevel4 { get => _wageLevels[4]; set => _wageLevels[4] = value; }

        [SettingsUISlider(min = 1000f, max = 8000f, step = 100f)]
        [SettingsUISection(WageSection, WageGroup)]
        public int wageLevel5 { get => _wageLevels[5]; set => _wageLevels[5] = value; }

        [SettingsUISlider(min = 0.25f, max = 5f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        [SettingsUISection(OtherSection, OtherGroup)]
        public float levelUpMaterialFactor { get; set; }

        private void SyncUIWithCaps()
        {
            levelCap1 = levelCaps[1];
            levelCap2 = levelCaps[2];
            levelCap3 = levelCaps[3];
            levelCap4 = levelCaps[4];
            levelCap5 = levelCaps[5];
        }


        private void ApplyCityCapPreset(CityOption city)
        {
            levelCaps = city switch
            {
                CityOption.Berlin => new float[] { 0, 0.15f, 0.25f, 0.35f, 0.2f, 0.05f },
                CityOption.SaoPaulo => new float[] { 0, 0.4f, 0.3f, 0.2f, 0.08f, 0.02f },
                CityOption.Tokyo => new float[] { 0, 0.1f, 0.2f, 0.4f, 0.2f, 0.1f },
                CityOption.London => new float[] { 0, 0.1f, 0.2f, 0.3f, 0.25f, 0.15f },
                CityOption.Cairo => new float[] { 0, 0.5f, 0.3f, 0.15f, 0.04f, 0.01f },
                CityOption.NewYork => new float[] { 0, 0.05f, 0.15f, 0.25f, 0.3f, 0.25f },
                CityOption.Mumbai => new float[] { 0, 0.45f, 0.3f, 0.15f, 0.08f, 0.02f },
                CityOption.Paris => new float[] { 0, 0.1f, 0.2f, 0.3f, 0.25f, 0.15f },
                CityOption.LosAngeles => new float[] { 0, 0.05f, 0.15f, 0.3f, 0.3f, 0.2f },
                CityOption.Moscow => new float[] { 0, 0.2f, 0.3f, 0.3f, 0.15f, 0.05f },
                CityOption.Seoul => new float[] { 0, 0.08f, 0.2f, 0.35f, 0.25f, 0.12f },
                CityOption.Istanbul => new float[] { 0, 0.25f, 0.3f, 0.25f, 0.15f, 0.05f },
                CityOption.Bangkok => new float[] { 0, 0.35f, 0.3f, 0.2f, 0.1f, 0.05f },
                CityOption.Sydney => new float[] { 0, 0.05f, 0.15f, 0.35f, 0.3f, 0.15f },
                CityOption.MexicoCity => new float[] { 0, 0.3f, 0.3f, 0.25f, 0.1f, 0.05f },
                CityOption.Toronto => new float[] { 0, 0.1f, 0.25f, 0.3f, 0.25f, 0.1f },
                CityOption.BuenosAires => new float[] { 0, 0.3f, 0.3f, 0.25f, 0.1f, 0.05f },
                CityOption.Beijing => new float[] { 0, 0.25f, 0.3f, 0.3f, 0.1f, 0.05f },
                CityOption.CapeTown => new float[] { 0, 0.4f, 0.3f, 0.2f, 0.08f, 0.02f },
                CityOption.RioDeJaneiro => new float[] { 0, 0.35f, 0.3f, 0.2f, 0.1f, 0.05f },
                CityOption.Chicago => new float[] { 0, 0.1f, 0.2f, 0.3f, 0.25f, 0.15f },
                CityOption.Santiago => new float[] { 0, 0.35f, 0.3f, 0.2f, 0.1f, 0.05f },
                _ => new float[] { 0, 0.3f, 0.3f, 0.2f, 0.15f, 0.05f },
            };

            SyncUIWithCaps();
        }

        private void ApplyCityWagePreset(CityOption city)
        {
            switch (city)
            {
                case CityOption.Berlin:
                    wageLevel1 = 1400; wageLevel2 = 1800; wageLevel3 = 2100; wageLevel4 = 2400; wageLevel5 = 2700; break;

                case CityOption.SaoPaulo:
                    wageLevel1 = 1000; wageLevel2 = 1300; wageLevel3 = 1600; wageLevel4 = 2000; wageLevel5 = 2500; break;

                case CityOption.Tokyo:
                    wageLevel1 = 1600; wageLevel2 = 1900; wageLevel3 = 2200; wageLevel4 = 2500; wageLevel5 = 2700; break;

                case CityOption.London:
                    wageLevel1 = 1500; wageLevel2 = 1800; wageLevel3 = 2100; wageLevel4 = 2400; wageLevel5 = 2700; break;

                case CityOption.Cairo:
                    wageLevel1 = 1000; wageLevel2 = 1100; wageLevel3 = 1300; wageLevel4 = 1600; wageLevel5 = 2000; break;

                case CityOption.NewYork:
                    wageLevel1 = 1400; wageLevel2 = 1800; wageLevel3 = 2200; wageLevel4 = 2600; wageLevel5 = 3000; break;

                case CityOption.Mumbai:
                    wageLevel1 = 1000; wageLevel2 = 1200; wageLevel3 = 1400; wageLevel4 = 1800; wageLevel5 = 2300; break;

                case CityOption.Paris:
                    wageLevel1 = 1400; wageLevel2 = 1800; wageLevel3 = 2100; wageLevel4 = 2400; wageLevel5 = 2700; break;

                case CityOption.LosAngeles:
                    wageLevel1 = 1300; wageLevel2 = 1700; wageLevel3 = 2100; wageLevel4 = 2500; wageLevel5 = 2900; break;

                case CityOption.Moscow:
                    wageLevel1 = 1200; wageLevel2 = 1500; wageLevel3 = 1800; wageLevel4 = 2100; wageLevel5 = 2500; break;

                case CityOption.Seoul:
                    wageLevel1 = 1500; wageLevel2 = 1800; wageLevel3 = 2100; wageLevel4 = 2400; wageLevel5 = 2700; break;

                case CityOption.Istanbul:
                    wageLevel1 = 1000; wageLevel2 = 1300; wageLevel3 = 1600; wageLevel4 = 2000; wageLevel5 = 2400; break;

                case CityOption.Bangkok:
                    wageLevel1 = 1000; wageLevel2 = 1200; wageLevel3 = 1500; wageLevel4 = 1800; wageLevel5 = 2200; break;

                case CityOption.Sydney:
                    wageLevel1 = 1500; wageLevel2 = 1800; wageLevel3 = 2100; wageLevel4 = 2400; wageLevel5 = 2700; break;

                case CityOption.MexicoCity:
                    wageLevel1 = 1000; wageLevel2 = 1300; wageLevel3 = 1600; wageLevel4 = 2000; wageLevel5 = 2400; break;

                case CityOption.Toronto:
                    wageLevel1 = 1400; wageLevel2 = 1700; wageLevel3 = 2000; wageLevel4 = 2300; wageLevel5 = 2600; break;

                case CityOption.BuenosAires:
                    wageLevel1 = 1000; wageLevel2 = 1300; wageLevel3 = 1600; wageLevel4 = 1900; wageLevel5 = 2300; break;

                case CityOption.Beijing:
                    wageLevel1 = 1300; wageLevel2 = 1600; wageLevel3 = 1900; wageLevel4 = 2200; wageLevel5 = 2500; break;

                case CityOption.CapeTown:
                    wageLevel1 = 1000; wageLevel2 = 1300; wageLevel3 = 1600; wageLevel4 = 1900; wageLevel5 = 2200; break;

                case CityOption.RioDeJaneiro:
                    wageLevel1 = 1000; wageLevel2 = 1300; wageLevel3 = 1600; wageLevel4 = 2000; wageLevel5 = 2400; break;

                case CityOption.Chicago:
                    wageLevel1 = 1200; wageLevel2 = 1800; wageLevel3 = 2400; wageLevel4 = 3000; wageLevel5 = 3600; break;

                case CityOption.Santiago:
                    wageLevel1 = 1000; wageLevel2 = 1220; wageLevel3 = 2227; wageLevel4 = 3500; wageLevel5 = 5000; break;
            }
        }


        public enum CityOption
        {
            Berlin, SaoPaulo, Tokyo, London, Cairo,
            NewYork, Mumbai, Paris, LosAngeles, Moscow,
            Seoul, Istanbul, Bangkok, Sydney, MexicoCity,
            Toronto, BuenosAires, Beijing, CapeTown, RioDeJaneiro,
            Chicago, Santiago
        }

        public class LocaleEN : IDictionarySource
        {
            private readonly Setting m_Setting;
            public LocaleEN(Setting setting) => m_Setting = setting;

            public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
            {
                var entries = new Dictionary<string, string>
                {
                    { m_Setting.GetSettingsLocaleID(), "Urban Inequality" },
                    { m_Setting.GetOptionTabLocaleID(IncomeSection), "Income" },
                    { m_Setting.GetOptionTabLocaleID(CitySection), "City" },
                    { m_Setting.GetOptionTabLocaleID(WageSection), "Wage" },
                    { m_Setting.GetOptionTabLocaleID(EducationSection), "Education" },
                    { m_Setting.GetOptionTabLocaleID(LevelCapSection), "Level Cap" },
                    { m_Setting.GetOptionTabLocaleID(OtherSection), "Other" },

                    { m_Setting.GetOptionGroupLocaleID(IncomeGroup), "Income" },
                    { m_Setting.GetOptionGroupLocaleID(EducationGroup), "Education" },
                    { m_Setting.GetOptionGroupLocaleID(LevelCapGroup), "Level Cap" },
                    { m_Setting.GetOptionGroupLocaleID(WageGroup), "Wage" },
                    { m_Setting.GetOptionGroupLocaleID(CityGroup), "City" },

                    { m_Setting.GetOptionLabelLocaleID(nameof(minIncomePenalty)), "Min. Income Penalty" },
                    { m_Setting.GetOptionDescLocaleID(nameof(minIncomePenalty)), "Penalty for the highest income group" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(maxIncomePenalty)), "Max. Income Penalty" },
                    { m_Setting.GetOptionDescLocaleID(nameof(maxIncomePenalty)), "Penalty for the lowest income group" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(minEducationPenalty)), "Min. Education Penalty" },
                    { m_Setting.GetOptionDescLocaleID(nameof(minEducationPenalty)), "Penalty for the highest education group" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(maxEducationPenalty)), "Max. Education Penalty" },
                    { m_Setting.GetOptionDescLocaleID(nameof(maxEducationPenalty)), "Penalty for the lowest education group" },

                    { m_Setting.GetOptionLabelLocaleID(nameof(selectedCity)), "City Settings" },
                    { m_Setting.GetOptionDescLocaleID(nameof(selectedCity)), "Choose a preset inequality distribution by city" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(ApplyPresetButton)), "Apply City Settings" },
                    { m_Setting.GetOptionDescLocaleID(nameof(ApplyPresetButton)), "Apply the selected city preset to level caps and wages" },

                    { m_Setting.GetOptionLabelLocaleID(nameof(levelCap1)), "Level 1 Cap" },
                    { m_Setting.GetOptionDescLocaleID(nameof(levelCap1)), "Max % of buildings at level 1" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(levelCap2)), "Level 2 Cap" },
                    { m_Setting.GetOptionDescLocaleID(nameof(levelCap2)), "Max % of buildings at level 2" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(levelCap3)), "Level 3 Cap" },
                    { m_Setting.GetOptionDescLocaleID(nameof(levelCap3)), "Max % of buildings at level 3" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(levelCap4)), "Level 4 Cap" },
                    { m_Setting.GetOptionDescLocaleID(nameof(levelCap4)), "Max % of buildings at level 4" },
                    { m_Setting.GetOptionLabelLocaleID(nameof(levelCap5)), "Level 5 Cap" },
                    { m_Setting.GetOptionDescLocaleID(nameof(levelCap5)), "Max % of buildings at level 5" },

                    { m_Setting.GetOptionLabelLocaleID(nameof(m_Setting.wageLevel1)), "Wage Level 1" },
                    { m_Setting.GetOptionDescLocaleID(nameof(m_Setting.wageLevel1)), "Monthly wage for the lowest level of residents." },
                    
                    { m_Setting.GetOptionLabelLocaleID(nameof(m_Setting.wageLevel2)), "Wage Level 2" },
                    { m_Setting.GetOptionDescLocaleID(nameof(m_Setting.wageLevel2)), "Monthly wage for lower working-class residents." },
                    
                    { m_Setting.GetOptionLabelLocaleID(nameof(m_Setting.wageLevel3)), "Wage Level 3" },
                    { m_Setting.GetOptionDescLocaleID(nameof(m_Setting.wageLevel3)), "Monthly wage for middle-class residents." },
                    
                    { m_Setting.GetOptionLabelLocaleID(nameof(m_Setting.wageLevel4)), "Wage Level 4" },
                    { m_Setting.GetOptionDescLocaleID(nameof(m_Setting.wageLevel4)), "Monthly wage for upper middle-class residents." },
                    
                    { m_Setting.GetOptionLabelLocaleID(nameof(m_Setting.wageLevel5)), "Wage Level 5" },
                    { m_Setting.GetOptionDescLocaleID(nameof(m_Setting.wageLevel5)), "Monthly wage for the highest income residents." },

                    { m_Setting.GetOptionLabelLocaleID(nameof(levelUpMaterialFactor)), "Level-Up Material Factor" },
                    { m_Setting.GetOptionDescLocaleID(nameof(levelUpMaterialFactor)), "Multiply the amount of materials required to level up." },

                };

                foreach (var city in Enum.GetValues(typeof(CityOption)))
                {
                    entries[m_Setting.GetEnumValueLocaleID((CityOption)city)] = city.ToString();
                }

                return entries;
            }

            public void Unload()
            {

            }
        }
    }
}

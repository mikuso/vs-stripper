using System;
using Vintagestory.API.Common;

namespace Stripper
{
    public class StripperConfigData
    {
        public bool EquipArmourOnTakeDamage = true;
        public float EquipArmourOnDamageThreshold = 1.0f;
    }

    public class StripperConfig
    {
        private const string Filename = "stripper.json";
        private ICoreAPI api;
        public StripperConfigData data;

        public StripperConfig(ICoreAPI api)
        {
            this.api = api;

            Load();
        }

        public void Save()
        {
            try
            {
                api.StoreModConfig<StripperConfigData>(data, Filename);
            } catch (Exception e)
            {
                // save failed? oh well.
            }
        }

        private void Load()
        {
            try
            {
                data = api.LoadModConfig<StripperConfigData>(Filename);
                if (data == null)
                {
                    data = new StripperConfigData();
                }
                Save();
            }
            catch (Exception e)
            {
                data = new StripperConfigData();
            }
        }
    }
}

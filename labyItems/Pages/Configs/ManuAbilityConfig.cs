using labyItems.Models;

namespace labyItems.Pages.Configs
{
    public class ManuAbilityConfig : ConfigBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int Table { get; set; }
        public int Cost { get; set; }
        public bool CanBuyMultiple { get; set; }

        public void ApplyManuAbility(MakeAbility.Result picked)
        {
            Name = picked.Name;
            Cost = picked.Cost;
            Description = picked.Description;
            Table = picked.Table;
        }
        protected override int ExtraTotal()
        {
            return 0;
        }
    }
}
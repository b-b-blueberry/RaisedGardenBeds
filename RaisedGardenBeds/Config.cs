namespace RaisedGardenBeds
{
	public class Config
	{
		public bool RaisedBedsMayBreakWithAge { get; set; } = true;
		public bool CanBePlacedInGreenHouse { get; set; } = true;
		public bool CanBePlacedInFarmHouse { get; set; } = false;
		public bool CanBePlacedInBuildings { get; set; } = false;
		public bool SprinklersEnabled { get; set; } = false;
		public bool RecipesAlwaysAvailable { get; set; } = false;
	}
}

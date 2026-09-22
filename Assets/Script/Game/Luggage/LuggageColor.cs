// A bag's flight colour. Colour is the whole destination rule in this design: a gate's
// flight asks for a number of bags of a colour, and any bag of that colour fills a slot.
// No bag is ever assigned to a particular gate.
//
// Set on the prefab — there is one luggage prefab variant per colour — and never changed
// at runtime.
public enum LuggageColor
{
    Red,
    Blue,
    Green,
    Yellow
}

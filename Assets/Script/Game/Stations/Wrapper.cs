// Wrapping station. Wraps any bag that is not already wrapped, *in place*, so the bag keeps its
// flight colour — a gate asking for "red, wrapped" still needs the bag to be red.
public class Wrapper : MachineStation
{
    public override bool CanAccept(Luggage luggage)
        => luggage != null && !luggage.IsWrapped;

    protected override Luggage OnProcessComplete(Luggage luggage)
    {
        luggage.MarkWrapped();
        return luggage;
    }
}

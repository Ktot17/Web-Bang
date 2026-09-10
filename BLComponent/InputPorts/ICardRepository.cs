namespace BLComponent.InputPorts;

public interface ICardRepository
{
    public IEnumerable<Card> GetAll { get; }
}

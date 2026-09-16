namespace Naval.Shared.Domain.Ai;

public interface IAiStrategy
{
    Coordinate ChooseTarget(PlayerState aiPlayer, PlayerState opponent);
}

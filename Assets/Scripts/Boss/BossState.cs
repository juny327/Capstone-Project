public abstract class BossState
{
    public abstract void Enter(BossBrain brain);
    public abstract void Tick(BossBrain brain);
    public abstract void Exit(BossBrain brain);
}
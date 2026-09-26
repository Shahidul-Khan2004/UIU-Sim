namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Senior student NPC. Attach to any humanoid student model; conversations are picked from the
    /// Senior pool of the <see cref="StudentDialogueDatabase"/>.
    /// </summary>
    public sealed class SeniorStudentNPC : StudentNPCBase
    {
        public override StudentNpcType NpcType => StudentNpcType.Senior;
        protected override string DefaultSpeakerName => "Senior Student";
    }
}

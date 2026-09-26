namespace UIU.Simulator.Gameplay.NPC
{
    /// <summary>
    /// Batchmate student NPC. Attach to any humanoid student model; conversations are picked from the
    /// Batchmate pool of the <see cref="StudentDialogueDatabase"/>.
    /// </summary>
    public sealed class BatchmateStudentNPC : StudentNPCBase
    {
        public override StudentNpcType NpcType => StudentNpcType.Batchmate;
        protected override string DefaultSpeakerName => "Batchmate";
    }
}

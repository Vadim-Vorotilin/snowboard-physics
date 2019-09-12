namespace SnowboardPhysics.Interfaces {
    
    /// <summary>
    /// This interface should be implemented in order to control a snowboard controller.
    /// </summary>
    public interface ISnowboardInput {
    
        /// <summary>
        /// Value from -1 to 1 that defines to which side the player turns.
        /// 1 means "right turn", -1 means "left turn", 0 means "no turn".
        /// This value isn't discrete and any real value in the range of [ -1; 1 ] can be applied.
        /// </summary>
        float Turning { get; }
    
        /// <summary>
        /// Value from -1 to 1 that defines how much the player tries to change their speed.
        /// -1 means "slowing down with maximum effort", 0 means "no effort is applied to change speed",
        /// 1 means "change direction to face the steepest slope".
        /// </summary>
        float Speeding { get; }
    
        /// <summary>
        /// Whether the player should jump in the current frame.
        /// </summary>
        bool Jump { get; }
    
    }
}
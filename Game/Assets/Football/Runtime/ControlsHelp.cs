namespace Football.Game
{
    /// <summary>Texto de ayuda de los controles (se muestra en Ajustes → Controles, nunca encima del partido).</summary>
    public static class ControlsHelp
    {
        /// <summary>Guía de controles táctiles y de teclado, con las teclas asignadas ahora.</summary>
        public static string Text()
        {
            string K(GameAction a) => KeyBindings.KeyName(KeyBindings.Get(a));
            return "TÁCTIL: mitad izquierda para mover. Tu jugador\nes el de la flecha amarilla.\n\n"
                   + "CON BALÓN\nPASE (toque: automático, mantener: potencia)\nTIRO (mantener y soltar) · DESPEJAR\n\n"
                   + "SIN BALÓN\nPRESIONAR y 2ª MARCA (mantener) · BARRER · CAMBIAR\nARQUERO (controlar al arquero)\n\n"
                   + "TECLADO\nMover " + K(GameAction.Up) + " " + K(GameAction.Left) + " " + K(GameAction.Down) + " " + K(GameAction.Right)
                   + " · Esprint " + K(GameAction.Sprint) + "\nPase " + K(GameAction.Pass) + " · Tiro " + K(GameAction.Shoot)
                   + " · Despejar " + K(GameAction.Clear) + "\nPresionar " + K(GameAction.Press) + " · Barrer " + K(GameAction.Slide)
                   + " · 2ª marca " + K(GameAction.SecondPress) + " · Cambiar " + K(GameAction.Switch) + "\nArquero " + K(GameAction.Keeper) + " · Pausa " + K(GameAction.Pause)
                   + "\n(se cambian en Ajustes → Controles de teclado)";
        }
    }
}

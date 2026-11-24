using System;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    // Toda la configuración del motor de partido vive aquí como datos. Son clases con campos públicos para que
    // se puedan cargar desde JSON (Data/balance/match.json) con JsonUtility en Unity o System.Text.Json fuera.
    // Las coordenadas: X a lo largo del campo, Y a lo ancho, Z altura; el centro del campo es (0, 0).

    /// <summary>Dimensiones del campo y de las porterías (metros).</summary>
    [Serializable]
    public sealed class PitchConfig
    {
        /// <summary>Largo del campo.</summary>
        public float Length = 105f;
        /// <summary>Ancho del campo.</summary>
        public float Width = 68f;
        /// <summary>Distancia entre postes.</summary>
        public float GoalWidth = 7.32f;
        /// <summary>Altura del larguero.</summary>
        public float GoalHeight = 2.44f;
        /// <summary>Profundidad del área grande.</summary>
        public float PenaltyAreaDepth = 16.5f;
        /// <summary>Mitad del ancho del área grande.</summary>
        public float PenaltyAreaHalfWidth = 20.16f;
        /// <summary>Profundidad del área chica.</summary>
        public float GoalAreaDepth = 5.5f;
        /// <summary>Distancia del punto de penalti a la línea de gol.</summary>
        public float PenaltySpotDistance = 11f;
        /// <summary>Radio del círculo central y distancia mínima en saques.</summary>
        public float CenterCircleRadius = 9.15f;
        /// <summary>Radio de los postes y del larguero.</summary>
        public float PostRadius = 0.06f;
        /// <summary>Margen fuera de las líneas donde pueden moverse los jugadores (negativo = no llegan a la línea).</summary>
        public float PlayerMargin = 2.5f;
        /// <summary>Mitad del ancho del área chica.</summary>
        public float GoalAreaHalfWidth = 9.16f;
        /// <summary>Cancha cerrada: paredes en las bandas y en el fondo (fuera de la portería) que devuelven el balón.</summary>
        public bool Walls;
        /// <summary>Fracción de la velocidad perpendicular que conserva el balón al rebotar en una pared.</summary>
        public float WallRestitution = 0.6f;
    }

    /// <summary>Efecto del clima en el juego (se aplica con <see cref="MatchConfig.ApplyWeather"/>).</summary>
    [Serializable]
    public sealed class WeatherConfig
    {
        /// <summary>Con lluvia el césped mojado frena menos: la fricción del balón se multiplica por esto (menor que 1: rueda más lejos).</summary>
        public float RainRollScale = 0.8f;
        /// <summary>Con lluvia los malos controles y toques son más probables (multiplica <see cref="PlayerConfig.MiscontrolChance"/> y <see cref="PlayerConfig.HeavyTouchRate"/>).</summary>
        public float RainMiscontrolScale = 1.3f;
    }

    /// <summary>Física del balón.</summary>
    [Serializable]
    public sealed class BallConfig
    {
        /// <summary>Radio del balón.</summary>
        public float Radius = 0.11f;
        /// <summary>Gravedad (m/s²).</summary>
        public float Gravity = 9.81f;
        /// <summary>Coeficiente de arrastre cuadrático en el aire (1/m).</summary>
        public float AirDrag = 0.012f;
        /// <summary>Deceleración constante al rodar sobre el césped (m/s²).</summary>
        public float RollDeceleration = 2.4f;
        /// <summary>Fricción de rodadura proporcional a la velocidad (1/s).</summary>
        public float RollDrag = 0.10f;
        /// <summary>Fracción de velocidad vertical que se conserva al rebotar.</summary>
        public float Restitution = 0.65f;
        /// <summary>Fracción de velocidad horizontal que se conserva al rebotar.</summary>
        public float BounceFriction = 0.82f;
        /// <summary>Por debajo de esta velocidad vertical el balón deja de rebotar.</summary>
        public float MinBounceSpeed = 1.2f;
        /// <summary>Aceleración lateral por efecto: coeficiente × giro × velocidad.</summary>
        public float MagnusCoefficient = 0.0045f;
        /// <summary>Pérdida de giro por segundo en el aire.</summary>
        public float SpinDecay = 0.35f;
        /// <summary>Pérdida de giro por segundo sobre el césped.</summary>
        public float GroundSpinDecay = 5f;
        /// <summary>Fracción de velocidad que se conserva al chocar con un poste o el larguero.</summary>
        public float WoodworkRestitution = 0.55f;
        /// <summary>Compensación del arrastre al calcular la velocidad de un pase en parábola.</summary>
        public float LobDragCompensation = 1.12f;
    }

    /// <summary>Movimiento, resistencia y control del balón de los jugadores.</summary>
    [Serializable]
    public sealed class PlayerConfig
    {
        /// <summary>Altura del balón (m) desde la que controlarlo es un cabezazo y no un control con el pie o el pecho (solo cambia la animación).</summary>
        public float HeaderHeight = 1.25f;
        /// <summary>Cuánto dura en pantalla la animación de primer toque o cabezazo (s).</summary>
        public float TouchDisplaySeconds = 0.4f;
        /// <summary>Velocidad punta con Pace = 1 (m/s).</summary>
        public float MinTopSpeed = 5.2f;
        /// <summary>Velocidad punta con Pace = 100 (m/s).</summary>
        public float MaxTopSpeed = 9.8f;
        /// <summary>Aceleración con Acceleration = 1 (m/s²).</summary>
        public float MinAcceleration = 3.8f;
        /// <summary>Aceleración con Acceleration = 100 (m/s²).</summary>
        public float MaxAcceleration = 7.4f;
        /// <summary>Frenada (m/s²).</summary>
        public float Deceleration = 8f;
        /// <summary>Velocidad de giro parado (rad/s).</summary>
        public float TurnRate = 8.5f;
        /// <summary>Cuánto reduce la velocidad la capacidad de girar.</summary>
        public float TurnSpeedPenalty = 0.09f;
        /// <summary>Fracción de la velocidad deseada cuando el jugador mira en dirección contraria.</summary>
        public float MisalignedSpeedFactor = 0.2f;
        /// <summary>Fracción de la velocidad punta al trotar.</summary>
        public float JogFraction = 0.6f;
        /// <summary>Gasto de resistencia por segundo a máxima velocidad.</summary>
        public float StaminaDrainSprint = 0.022f;
        /// <summary>Gasto de resistencia por segundo al trotar.</summary>
        public float StaminaDrainJog = 0.00264f;
        /// <summary>Recuperación de resistencia por segundo parado.</summary>
        public float StaminaRecovery = 0.003f;
        /// <summary>Fracción de la velocidad que se conserva con la resistencia agotada.</summary>
        public float StaminaSpeedFloor = 0.82f;
        /// <summary>Variación del gasto de resistencia según el atributo Stamina (±).</summary>
        public float StaminaAttributeSpread = 0.5f;
        /// <summary>Fracción de la aceleración que se conserva con la resistencia agotada.</summary>
        public float StaminaAccelerationFloor = 0.8f;
        /// <summary>Aumento del error de pase, centro y tiro con la resistencia agotada (×(1 + este valor × cansancio)).</summary>
        public float FatigueErrorWeight = 0.6f;
        /// <summary>Aumento del tiempo de reacción (decisiones) con la resistencia agotada (×(1 + este valor × cansancio)).</summary>
        public float FatigueReactionWeight = 0.5f;
        /// <summary>Minuto a partir del cual puede haber lesiones por cansancio.</summary>
        public float InjuryStartMinute = 70f;
        /// <summary>Probabilidad de lesión leve por jugador y minuto de juego, descansado.</summary>
        public float InjuryBaseRate = 0.0002f;
        /// <summary>Cuánto multiplica el cansancio esa probabilidad (×(1 + este valor × cansancio²)).</summary>
        public float InjuryFatigueWeight = 25f;
        /// <summary>Fracción de la velocidad punta de un jugador lesionado que sigue en el campo.</summary>
        public float InjurySpeedFactor = 0.7f;
        /// <summary>Distancia mínima entre jugadores.</summary>
        public float SeparationDistance = 0.8f;
        /// <summary>Velocidad de choque entre rivales a partir de la cual el más débil trastabilla (m/s).</summary>
        public float StumbleMinImpact = 3f;
        /// <summary>Duración del trastabilleo (s).</summary>
        public float StumbleDuration = 0.45f;
        /// <summary>Fracción de la velocidad que conserva quien trastabilla.</summary>
        public float StumbleSpeedFactor = 0.45f;
        /// <summary>Probabilidad de que el poseedor pierda el balón al trastabillar.</summary>
        public float StumbleLoseBall = 0.3f;
        /// <summary>Cuánto pesa la fuerza al decidir quién gana el choque (0 = azar puro).</summary>
        public float StumbleStrengthWeight = 1.5f;
        /// <summary>Radio dentro del cual el jugador reduce la velocidad al llegar a su objetivo.</summary>
        public float ArriveRadius = 1.5f;
        /// <summary>Radio dentro del cual el jugador se detiene en su objetivo.</summary>
        public float StopRadius = 0.25f;

        /// <summary>Distancia del balón al jugador cuando lo conduce.</summary>
        public float CarryDistance = 0.55f;
        /// <summary>Radio en el que un jugador controla un balón suelto.</summary>
        public float ControlRadius = 0.9f;
        /// <summary>Altura máxima a la que se controla el balón.</summary>
        public float ControlMaxHeight = 1.8f;
        /// <summary>Velocidad máxima del balón que se puede controlar con FirstTouch medio (m/s).</summary>
        public float ControlMaxBallSpeed = 15f;
        /// <summary>Variación de esa velocidad según FirstTouch (±fracción).</summary>
        public float ControlSpeedSpread = 0.35f;
        /// <summary>
        /// Probabilidad de un mal control (el balón se le escapa hacia delante) con un balón al límite de lo controlable y
        /// FirstTouch medio. Crece con el cuadrado de la velocidad relativa y baja con FirstTouch.
        /// </summary>
        public float MiscontrolChance = 0.6f;
        /// <summary>Peso de FirstTouch en el mal control: con FirstTouch máximo la probabilidad se multiplica por (1 - este valor).</summary>
        public float MiscontrolSkillWeight = 0.8f;
        /// <summary>Distancia de un rival que presiona al receptor y le complica el control (m).</summary>
        public float MiscontrolPressureRange = 2.5f;
        /// <summary>Aumento de la probabilidad de mal control con un rival encima (fracción).</summary>
        public float MiscontrolPressureWeight = 0.6f;
        /// <summary>Velocidad que conserva el balón tras un mal control (fracción).</summary>
        public float MiscontrolSpeedFactor = 0.45f;
        /// <summary>Desvío aleatorio del balón tras un mal control (rad, desviación típica).</summary>
        public float MiscontrolNoise = 0.5f;
        /// <summary>Radio en el que un jugador bloquea un balón rápido.</summary>
        public float BlockRadius = 0.55f;
        /// <summary>Fracción de velocidad que conserva un balón que rebota en un jugador.</summary>
        public float DeflectionSpeedFactor = 0.45f;
        /// <summary>Dispersión angular del rebote (rad).</summary>
        public float DeflectionNoise = 0.6f;
        /// <summary>Velocidad vertical máxima de un rebote (m/s).</summary>
        public float DeflectionLift = 2f;
        /// <summary>Velocidad hacia la línea de fondo de un manotazo por encima del larguero (m/s).</summary>
        public float ParryOverBarForward = 3f;
        /// <summary>Velocidad vertical inicial que se prueba para ese manotazo (m/s).</summary>
        public float ParryOverBarLift = 8f;
        /// <summary>Altura que el manotazo deja por encima del larguero (m).</summary>
        public float ParryBarMargin = 0.5f;
        /// <summary>Velocidad vertical máxima que puede dar el arquero a un manotazo (m/s).</summary>
        public float ParryOverBarMaxLift = 14f;
        /// <summary>Distancia más allá de la línea de gol a la que debe pasar ese manotazo (m).</summary>
        public float ParryLineMargin = 0.4f;
        /// <summary>Velocidad de un pase de la persona con potencia mínima, respecto de la que calcula la asistencia.</summary>
        public float HumanPassPowerMin = 0.8f;
        /// <summary>Velocidad de un pase de la persona con potencia máxima, respecto de la que calcula la asistencia.</summary>
        public float HumanPassPowerMax = 1.6f;
        /// <summary>Tiempo que se guarda un botón de golpeo pulsado justo antes de poder usarlo (s).</summary>
        public float HumanInputBuffer = 0.2f;
        /// <summary>Tiempo que se guardan la entrada y la barrida pulsadas mientras el jugador termina otra acción (s).</summary>
        public float HumanDefendBuffer = 0.18f;
        /// <summary>Segunda marca: distancia máxima al poseedor rival del compañero que sale a presionar (m).</summary>
        public float SecondPressMaxDistance = 28f;
        /// <summary>Segunda marca: gasto extra de resistencia por segundo del compañero que presiona (además de correr).</summary>
        public float SecondPressStaminaDrain = 0.004f;
        /// <summary>Altura a la que el arquero sostiene el balón en las manos (m).</summary>
        public float KeeperHoldHeight = 1.2f;
        /// <summary>Cuánto adelante del cuerpo (m) sostiene el arquero el balón: contra el pecho, no encima de la barriga.</summary>
        public float KeeperHoldForward = 0.32f;
        /// <summary>Una vez lanzado, el arquero mantiene su estirada salvo que el punto de llegada cambie más de esto (m): no se corrige en el aire.</summary>
        public float KeeperDiveRetarget = 1.2f;
        /// <summary>Distancia mínima del arquero con balón a su línea de gol (m): nunca la cruza con el balón en las manos.</summary>
        public float KeeperLineInset = 0.5f;
        /// <summary>Tras patear, el jugador no puede recuperar el balón durante este tiempo (s).</summary>
        public float KickCooldown = 0.5f;
        /// <summary>Tiempo tras controlar el balón antes de poder actuar (s).</summary>
        public float ControlDelay = 0.18f;
        /// <summary>
        /// Pase de primera: si la persona pulsa un botón de golpeo mientras un pase de un compañero viene hacia ella, la orden espera
        /// este tiempo (s) a que el balón llegue, en vez del instante normal de <see cref="HumanInputBuffer"/>.
        /// </summary>
        public float HumanFirstTimeBuffer = 1.5f;
        /// <summary>Con una orden de golpeo ya pedida antes de recibir, el jugador espera solo esto (s) tras controlar el balón: pasa al primer toque.</summary>
        public float FirstTimeControlDelay = 0.03f;
        /// <summary>Probabilidad por segundo de un mal toque a máxima velocidad con habilidad mínima.</summary>
        public float HeavyTouchRate = 0.45f;
        /// <summary>Velocidad extra del balón en un mal toque (m/s).</summary>
        public float HeavyTouchSpeed = 4.5f;
        /// <summary>
        /// Mal toque de quien controla la persona: solo ocurre con un rival a menos de <see cref="HumanHeavyTouchRange"/> (con la cancha libre
        /// la pelota nunca se escapa sola: la persona no pidió soltarla) y con esta proporción de la probabilidad normal.
        /// </summary>
        public float HumanHeavyTouchScale = 0.5f;
        /// <summary>Distancia (m) a la que un rival hace posible un mal toque del jugador controlado por la persona.</summary>
        public float HumanHeavyTouchRange = 4f;

        /// <summary>Velocidad lateral del portero al lanzarse (m/s).</summary>
        public float KeeperDiveSpeed = 6.5f;
        /// <summary>Alcance del portero con Reflexes = 1 (m).</summary>
        public float KeeperBaseReach = 1.35f;
        /// <summary>Alcance adicional con Reflexes = 100 (m).</summary>
        public float KeeperExtraReach = 1.5f;
        /// <summary>Altura del centro de la esfera de alcance del portero (m).</summary>
        public float KeeperReachHeight = 1.0f;
        /// <summary>Probabilidad base de parada cuando el balón entra en el alcance.</summary>
        public float KeeperSaveBase = 0.45f;
        /// <summary>Variación de la parada según la habilidad del portero (±).</summary>
        public float KeeperSaveSkillSpread = 0.30f;
        /// <summary>Velocidad de tiro a partir de la cual la parada se vuelve más difícil (m/s).</summary>
        public float KeeperSaveSpeedRef = 14f;
        /// <summary>Penalización de la parada por cada m/s sobre la referencia.</summary>
        public float KeeperSaveSpeedPenalty = 0.015f;
        /// <summary>Cuánto baja la atajada si el balón pasa al límite de su alcance (pegado al palo) en vez de al cuerpo.</summary>
        public float KeeperSaveEdgePenalty = 0.8f;
        /// <summary>
        /// Tiempo de reacción del arquero (s). El tiempo que tiene ante un remate es distancia ÷ velocidad; la «urgencia»
        /// es este valor dividido por ese tiempo (1 = no llega a reaccionar): mano a mano y remates potentes en el área.
        /// </summary>
        public float KeeperReactionTime = 0.5f;
        /// <summary>Atajada que se pierde con urgencia máxima.</summary>
        public float KeeperUrgencyPenalty = 0.8f;
        /// <summary>Parte de esa pérdida que sufre también un remate directo al cuerpo (el resto crece hacia el palo).</summary>
        public float KeeperUrgencyBodyShare = 0.25f;
        /// <summary>Tiempo a partir del cual el arquero va cómodo (s): remates lejanos o lentos.</summary>
        public float KeeperComfortTime = 0.8f;
        /// <summary>Segundos más allá de ese tiempo en los que la comodidad llega al máximo.</summary>
        public float KeeperComfortRange = 0.8f;
        /// <summary>Atajada extra con comodidad máxima.</summary>
        public float KeeperComfortBonus = 0.6f;
        /// <summary>Alcance extra con comodidad máxima (fracción): llega a tiros colocados que de cerca no alcanza.</summary>
        public float KeeperComfortReach = 1.2f;
        /// <summary>Velocidad máxima del balón que el portero puede atrapar (m/s).</summary>
        public float KeeperCatchMaxSpeed = 17f;
        /// <summary>Velocidad mínima del balón para considerarlo un tiro a portería (m/s).</summary>
        public float ShotMinSpeed = 11f;
    }

    /// <summary>Efecto de los roles individuales en el posicionamiento sin balón (x en fracción del largo, y en metros o factor).</summary>
    [Serializable]
    public sealed class RoleConfig
    {
        /// <summary>Lateral defensivo: cuánto menos sube en ataque (fracción del largo).</summary>
        public float DefensiveHold = 0.08f;
        /// <summary>Lateral defensivo: factor de su apertura (menos de 1 = cierra por dentro).</summary>
        public float DefensiveWidth = 0.85f;
        /// <summary>Carrilero: cuánto más sube en ataque.</summary>
        public float WingBackAdvance = 0.10f;
        /// <summary>Carrilero: factor de su apertura en ataque.</summary>
        public float WingBackWidth = 1.2f;
        /// <summary>Recuperador: cuánto se acerca al balón al defender (fracción de la distancia).</summary>
        public float BallWinnerPull = 0.3f;
        /// <summary>Recuperador: cuánto menos sube en ataque.</summary>
        public float BallWinnerHold = 0.05f;
        /// <summary>Organizador: cuánto se acerca al balón en ataque (fracción de la distancia).</summary>
        public float PlaymakerPull = 0.25f;
        /// <summary>Llegador: cuánto más sube en ataque.</summary>
        public float BoxToBoxAdvance = 0.10f;
        /// <summary>Pivote: factor de su apertura (centrado).</summary>
        public float TargetManWidth = 0.4f;
        /// <summary>Pivote: cuánto baja a recibir en ataque.</summary>
        public float TargetManDrop = 0.04f;
        /// <summary>Al espacio: cuánto más se adelanta en ataque (siempre sin pasar la línea del fuera de juego).</summary>
        public float RunnerAdvance = 0.08f;
    }

    /// <summary>Director técnico en vivo: gritos desde el banco y charla del entretiempo.</summary>
    [Serializable]
    public sealed class CoachConfig
    {
        /// <summary>Más probabilidad de falta por cada nivel de rivalidad (rivalidad 1, derbi 2, clásico 3).</summary>
        public float RivalryFoulStep = 0.15f;
        /// <summary>Más error de golpeo por cada nivel de rivalidad, para un jugador sin serenidad (Composure).</summary>
        public float RivalryPressureStep = 0.05f;
        /// <summary>Cuánto dura un grito táctico (s de simulación; el partido dura 6 min).</summary>
        public float ShoutSeconds = 45f;
        /// <summary>Tamaño del ajuste de estilo de un grito (fracción en cada eje que toca).</summary>
        public float ShoutStep = 0.3f;
        /// <summary>Saques más lentos con «perder tiempo» (× la espera normal).</summary>
        public float WasteTimeDelayFactor = 1.6f;
        /// <summary>Menos error de pase y tiro por cada punto de efecto de la charla (positivo = mejor).</summary>
        public float TalkErrorStep = 0.08f;
        /// <summary>Más intensidad de presión por cada punto de efecto de la charla.</summary>
        public float TalkPressStep = 0.1f;
    }

    /// <summary>Reglas, duración y árbitro.</summary>
    [Serializable]
    public sealed class RulesConfig
    {
        /// <summary>Duración real de cada parte (s).</summary>
        public float HalfDuration = 180f;
        /// <summary>Minutos de partido que representa cada parte (también en el 3 contra 3).</summary>
        public float MinutesPerHalf = 45f;
        /// <summary>
        /// Duración real de una parte (s) con la que se calibró el gasto de resistencia. El desgaste se mide en minutos de partido: una parte
        /// más corta gasta más por segundo real, para que a los 75' el cansancio sea el mismo con 2 que con 5 minutos por parte.
        /// </summary>
        public float FatigueReferenceHalf = 180f;
        /// <summary>Multiplicador del desgaste sobre lo calibrado (1 = el de antes; más alto, jugadores más cansados al final).</summary>
        public float FatigueFactor = 1.25f;
        /// <summary>Minutos que suma al final de la parte cada gol (festejo y saque del medio).</summary>
        public float StoppagePerGoal = 0.75f;
        /// <summary>Minutos que suma cada cambio.</summary>
        public float StoppagePerSubstitution = 0.5f;
        /// <summary>Minutos que suma cada lesión.</summary>
        public float StoppagePerInjury = 1.5f;
        /// <summary>Minutos que suma cada expulsión.</summary>
        public float StoppagePerRedCard = 1f;
        /// <summary>Minutos que suma cada amarilla.</summary>
        public float StoppagePerYellowCard = 0.25f;
        /// <summary>Minutos que suma cada penal (ejecución y reclamos).</summary>
        public float StoppagePerPenalty = 1f;
        /// <summary>
        /// Pasado el tiempo agregado, la jugada en curso se deja terminar. Los laterales y saques de arco cortan enseguida. Si el juego
        /// sigue después de estos minutos de partido y el balón está lejos de las dos áreas, se corta; con una jugada de gol (balón
        /// cerca de un área, tiro en el aire, córner o tiro libre) se espera.
        /// </summary>
        public float EndPlaySoftMinutes = 0.75f;
        /// <summary>Seguro: si una jugada de gol sigue sin terminar pasados estos minutos de partido, la parte termina igual (nunca con un penal).</summary>
        public float EndPlayHardMinutes = 6f;
        /// <summary>Un balón a menos de esta fracción de la mitad del largo de la cancha de un arco (0,5 ≈ 26 m en 105 m) es una jugada de gol.</summary>
        public float DangerZoneFraction = 0.5f;
        /// <summary>Mínimo de minutos agregados que se anuncian al final de cada parte.</summary>
        public int MinAddedMinutes = 1;
        /// <summary>Máximo de minutos agregados.</summary>
        public int MaxAddedMinutes = 7;
        /// <summary>Espera antes de ejecutar un saque (s): tiro libre y otros.</summary>
        public float RestartDelay = 1.4f;
        /// <summary>Espera antes de un saque lateral, para que los compañeros se acerquen (s).</summary>
        public float ThrowInDelay = 1.6f;
        /// <summary>Espera antes de un saque de arco, para que el equipo se abra (s).</summary>
        public float GoalKickDelay = 2.0f;
        /// <summary>Espera antes de un córner, para que todos entren al área (s).</summary>
        public float CornerDelay = 2.4f;
        /// <summary>Ángulo máximo del saque del medio respecto del frente (grados): hacia delante o al costado.</summary>
        public float KickoffAimDeg = 100f;
        /// <summary>Ángulo máximo del lateral respecto de la perpendicular a la banda (grados): siempre hacia adentro.</summary>
        public float ThrowInAimDeg = 80f;
        /// <summary>Ángulo máximo del córner respecto de la diagonal hacia el campo (grados).</summary>
        public float CornerAimDeg = 45f;
        /// <summary>Ángulo máximo del saque de arco respecto del frente (grados).</summary>
        public float GoalKickAimDeg = 100f;
        /// <summary>Distancia de un centro de pelota parada con potencia mínima y máxima (m).</summary>
        public float SetPieceCrossMin = 12f;
        /// <summary>Distancia de un centro de pelota parada con potencia máxima (m).</summary>
        public float SetPieceCrossMax = 45f;
        /// <summary>Distancia de un pelotazo largo (botón TIRO en saques que no son tiro libre) con potencia mínima (m).</summary>
        public float SetPieceLongMin = 30f;
        /// <summary>Distancia de ese pelotazo con potencia máxima (m).</summary>
        public float SetPieceLongMax = 62f;
        /// <summary>Si la persona no saca en este tiempo, saca la IA por ella (s).</summary>
        public float HumanRestartTimeout = 8f;
        /// <summary>En un córner de la persona, un pase a un compañero a menos de esta distancia sale corto y raso (m).</summary>
        public float ShortCornerDistance = 14f;
        /// <summary>Espera antes del saque inicial (s).</summary>
        public float KickoffDelay = 1.2f;
        /// <summary>Pausa tras un gol (s).</summary>
        public float GoalPauseDuration = 2.5f;
        /// <summary>Tiempo máximo que se espera a que el ejecutor llegue al balón (s).</summary>
        public float TakerTimeout = 4f;
        /// <summary>Distancia al balón a la que se considera que el ejecutor ya está listo (m).</summary>
        public float TakerReach = 2.2f;
        /// <summary>Distancia mínima del rival en saques.</summary>
        public float SetPieceDistance = 9.15f;
        /// <summary>Duración de una barrida (s).</summary>
        public float SlideDuration = 0.55f;
        /// <summary>Velocidad inicial de la barrida (m/s).</summary>
        public float SlideSpeed = 7.5f;
        /// <summary>Frenada durante la barrida (m/s²).</summary>
        public float SlideDeceleration = 9f;
        /// <summary>Alcance de la barrida (m); mayor que el de la entrada de pie.</summary>
        public float SlideReach = 1.8f;
        /// <summary>Tiempo en el suelo tras la barrida (s).</summary>
        public float SlideRecovery = 0.6f;
        /// <summary>Ángulo de salida del balón en un saque de banda (grados).</summary>
        public float ThrowInLoftDeg = 28f;
        /// <summary>Altura desde la que sale el balón en un saque de banda (metros): por encima de la cabeza.</summary>
        public float ThrowInReleaseHeight = 2f;
        /// <summary>Probabilidad extra de ganar el balón con una barrida.</summary>
        public float SlideWinBonus = 0.15f;
        /// <summary>Multiplicador de la probabilidad de falta de una barrida.</summary>
        public float SlideFoulMultiplier = 2f;
        /// <summary>Multiplicador de falta si la entrada es por detrás.</summary>
        public float BehindFoulMultiplier = 1.5f;
        /// <summary>Velocidad con la que sale el balón tras una barrida que lo toca (m/s).</summary>
        public float SlideKnockSpeed = 6f;
        /// <summary>Alcance de una entrada.</summary>
        public float TackleRange = 1.3f;
        /// <summary>Tiempo entre entradas de un mismo jugador (s).</summary>
        public float TackleCooldown = 1.0f;
        /// <summary>Tiempo entre entradas del jugador que controla la persona (s): más corto para que presionar responda.</summary>
        public float HumanTackleCooldown = 0.6f;
        /// <summary>Probabilidad extra de ganar el balón en las entradas de la persona (ayuda de control táctil).</summary>
        public float HumanTackleBonus = 0.10f;
        /// <summary>Probabilidad base de ganar el balón en una entrada.</summary>
        public float TackleWinBase = 0.28f;
        /// <summary>Peso de la diferencia Tackling − Dribbling en la entrada.</summary>
        public float TackleSkillWeight = 0.5f;
        /// <summary>Probabilidad extra (±) según si el defensor mira al poseedor (+1) o le da la espalda (−1).</summary>
        public float TackleFacingWeight = 0.12f;
        /// <summary>Probabilidad extra (±) según si el defensor está delante del poseedor (+1) o lo persigue por detrás (−1).</summary>
        public float TackleFrontWeight = 0.08f;
        /// <summary>Probabilidad que resta un recorte recién hecho por el poseedor (con regate máximo).</summary>
        public float CutEvadeBonus = 0.25f;
        /// <summary>Tiempo durante el que un recorte deja atrás al defensor (s).</summary>
        public float CutWindow = 0.4f;
        /// <summary>Giro mínimo dentro de un intervalo de muestreo para contar como recorte (grados).</summary>
        public float CutAngleDeg = 40f;
        /// <summary>Intervalo con el que se mide el giro del poseedor (s).</summary>
        public float CutSampleInterval = 0.25f;
        /// <summary>Velocidad mínima del poseedor para que un giro cuente como recorte (m/s).</summary>
        public float CutMinSpeed = 2.5f;
        /// <summary>Por debajo de este valor de "frente" la entrada se considera por detrás.</summary>
        public float BehindThreshold = -0.3f;
        /// <summary>A partir de este valor de "frente" (y de orientación del defensor) la entrada se considera de frente.</summary>
        public float FrontThreshold = 0.5f;
        /// <summary>Multiplicador de falta de una entrada de pie de frente y mirando al poseedor (la entrada limpia).</summary>
        public float FrontTackleFoulMultiplier = 0.5f;
        /// <summary>Multiplicador de falta de una entrada de pie por detrás.</summary>
        public float StandingBehindFoulMultiplier = 1.3f;
        /// <summary>Probabilidad base de falta en una entrada.</summary>
        public float FoulBase = 0.16f;
        /// <summary>Probabilidad base de tarjeta amarilla en una falta.</summary>
        public float YellowBase = 0.14f;
        /// <summary>Aumento de la amarilla según la agresividad (±).</summary>
        public float YellowAggressionSpread = 0.25f;
        /// <summary>Probabilidad de roja directa en una falta.</summary>
        public float RedBase = 0.008f;
        /// <summary>Cambios máximos por equipo (reglamento: 5).</summary>
        public int MaxSubstitutions = 5;
        /// <summary>Ventanas de cambios durante el juego (reglamento: 3; los del entretiempo no cuentan).</summary>
        public int MaxSubstitutionWindows = 3;
        /// <summary>Minuto desde el que la IA hace cambios para refrescar a los cansados.</summary>
        public float AiSubstitutionMinute = 58f;
        /// <summary>La IA cambia a un jugador cuya resistencia baje de este valor.</summary>
        public float AiSubstitutionStamina = 0.6f;
        /// <summary>Minuto desde el que la IA rota jugadores aunque nadie esté agotado (como un técnico real).</summary>
        public float AiRotationMinute = 65f;
        /// <summary>Cambios que la IA hace por rotación (los más cansados primero).</summary>
        public int AiRotationSubstitutions = 3;
        /// <summary>Se aplica la regla del fuera de juego (no existe en el fútbol sala).</summary>
        public bool Offside = true;
        /// <summary>Distancia máxima a portería para tirar una falta directa.</summary>
        public float FreeKickDirectMaxDistance = 30f;
    }

    /// <summary>Parámetros de la inteligencia artificial de los jugadores.</summary>
    [Serializable]
    public sealed class AiConfig
    {
        /// <summary>Intervalo entre decisiones de movimiento sin balón (s).</summary>
        public float DecisionInterval = 0.18f;
        /// <summary>Intervalo entre decisiones del jugador con balón (s).</summary>
        public float OwnerDecisionInterval = 0.22f;

        /// <summary>Zona de disparo: dentro de esta distancia al arco la IA prioriza patear sobre regatear (m).</summary>
        public float ShootZoneDistance = 22f;
        /// <summary>Un tiro tiene línea limpia si ningún jugador de campo rival está a menos de esta distancia del trayecto (m).</summary>
        public float ShotLaneClearance = 1.2f;
        /// <summary>En la zona de disparo, con un rival a menos de esta distancia, la IA patea antes de perder el balón (m).</summary>
        public float ShotPressureDistance = 2f;
        /// <summary>Tiempo máximo con el balón dentro del área rival antes de forzar el disparo (s).</summary>
        public float MaxBoxSeconds = 1.5f;
        /// <summary>Probabilidad de gol mínima para patear por las reglas de la zona (evita tirar desde un ángulo imposible).</summary>
        public float ZoneShotMinXg = 0.025f;
        /// <summary>Probabilidad de gol mínima para el disparo forzado por tiempo en el área (solo descarta ángulos nulos).</summary>
        public float StallShotMinXg = 0.004f;
        /// <summary>Fuerza del tiro de la IA a quemarropa; crece hasta 1 en el límite de la zona de disparo.</summary>
        public float ShotPowerClose = 0.7f;

        /// <summary>Distancia máxima a portería para considerar un tiro (m).</summary>
        public float ShootMaxRange = 30f;
        /// <summary>Escala de la probabilidad de gol esperada.</summary>
        public float XgScale = 0.75f;
        /// <summary>Distancia a la que la probabilidad cae a 1/e (m).</summary>
        public float XgDecay = 7.5f;
        /// <summary>Valor de la posesión tras un tiro fallado.</summary>
        public float ShotMissValue = 0.045f;
        /// <summary>Multiplicador de las ganas de tirar (juego de fútbol: más tiros que en la realidad por menos tiempo de partido).</summary>
        public float ShotEagerness = 1.6f;

        /// <summary>Valor mínimo de tener el balón en cualquier punto.</summary>
        public float ThreatBase = 0.06f;
        /// <summary>Valor adicional de tener el balón junto a la portería rival.</summary>
        public float ThreatScale = 0.30f;
        /// <summary>Exponente del avance hacia la portería rival.</summary>
        public float ThreatExponent = 4f;
        /// <summary>Pérdida de valor por estar en las bandas.</summary>
        public float ThreatCentralityLoss = 0.5f;
        /// <summary>Peso de perder el balón (valor de la posesión rival en ese punto).</summary>
        public float LossWeight = 0.8f;
        /// <summary>Premio de utilidad a pasarle a un compañero solo (sin rival cerca de donde recibe): se reparte entre los pases, centrado en el promedio, según qué tan libre está cada uno.</summary>
        public float FreeTeammateWeight = 0.06f;
        /// <summary>Distancia (m) al rival más cercano desde la que un compañero cuenta como totalmente libre para <see cref="FreeTeammateWeight"/>.</summary>
        public float FreeTeammateRange = 8f;

        /// <summary>Probabilidad base de pase con Passing = 1.</summary>
        public float PassBase = 0.55f;
        /// <summary>Aumento de esa probabilidad hasta Passing = 100.</summary>
        public float PassSkillSpread = 0.40f;
        /// <summary>Distancia de referencia para el descenso de acierto (m).</summary>
        public float PassDistanceScale = 34f;
        /// <summary>Margen de tiempo (s) del que dispone el balón sobre el rival más rápido en llegar al carril para considerarlo limpio.</summary>
        public float PassLaneSlackRange = 0.8f;
        /// <summary>Alcance con el que un rival corta un pase al llegar al carril (m).</summary>
        public float InterceptReach = 0.9f;
        /// <summary>Fracción de su velocidad punta con la que se supone que corre un rival a cortar un pase.</summary>
        public float InterceptRunFactor = 0.85f;
        /// <summary>Tiempo de reacción que se concede a un rival al cortar un pase (s).</summary>
        public float InterceptReaction = 0.15f;
        /// <summary>Un rival a esta distancia del carril o menos bloquea el pase sin necesidad de reaccionar (m).</summary>
        public float BlockReach = 0.8f;
        /// <summary>Distancia mínima de un pase (m).</summary>
        public float PassMinDistance = 4f;
        /// <summary>Distancia máxima de un pase (m).</summary>
        public float PassMaxDistance = 45f;
        /// <summary>Velocidad con la que el balón llega al receptor (m/s).</summary>
        public float PassArrivalSpeed = 7f;
        /// <summary>Velocidad mínima de un pase raso (m/s).</summary>
        public float PassMinSpeed = 8f;
        /// <summary>Velocidad máxima de un pase raso (m/s).</summary>
        public float PassMaxSpeed = 26f;
        /// <summary>Error angular típico de un pase con Passing = 1 (grados).</summary>
        public float PassAngleErrorDeg = 7f;
        /// <summary>Error relativo de velocidad de un pase.</summary>
        public float PassSpeedError = 0.05f;
        /// <summary>Segundos por delante del receptor a los que se apunta el pase.</summary>
        public float PassLeadTime = 0.5f;
        /// <summary>Distancia a partir de la cual un pase sale por alto (m).</summary>
        public float LobPassDistance = 34f;
        /// <summary>Ángulo de salida de un pase por alto (grados).</summary>
        public float LobLoftDeg = 32f;

        /// <summary>Velocidad mínima de un tiro (m/s).</summary>
        public float ShotSpeedMin = 20f;
        /// <summary>Velocidad máxima de un tiro (m/s).</summary>
        public float ShotSpeedMax = 31f;
        /// <summary>Error angular típico de un tiro con Shooting = 1 (grados).</summary>
        public float ShotAngleErrorDeg = 12f;
        /// <summary>Error típico de la elevación (grados).</summary>
        public float ShotLoftErrorDeg = 4f;
        /// <summary>Margen respecto al poste al apuntar a una esquina (m).</summary>
        public float ShotCornerMargin = 0.8f;
        /// <summary>Giro máximo que se da a un tiro con efecto (rad/s).</summary>
        public float ShotMaxSpin = 40f;

        /// <summary>Ángulo de salida de un centro (grados).</summary>
        public float CrossLoftDeg = 36f;
        /// <summary>Dispersión del punto de destino de un centro (m).</summary>
        public float CrossTargetSpread = 4f;
        /// <summary>Probabilidad base de que un centro llegue a un compañero.</summary>
        public float CrossBase = 0.30f;
        /// <summary>Valor de un centro que llega al área.</summary>
        public float CrossBoxValue = 0.09f;
        /// <summary>Distancia a la banda a partir de la cual se considera centrar (m).</summary>
        public float CrossWideDistance = 14f;
        /// <summary>Progreso mínimo (0..1) desde el que se considera centrar.</summary>
        public float CrossMinProgress = 0.72f;

        /// <summary>Probabilidad base de regate con Dribbling = 1.</summary>
        public float DribbleBase = 0.35f;
        /// <summary>Aumento de esa probabilidad hasta Dribbling = 100.</summary>
        public float DribbleSkillSpread = 0.50f;
        /// <summary>Distancia a la que se evalúa la conducción (m).</summary>
        public float DribbleLookahead = 6f;
        /// <summary>Distancia al rival más cercano a partir de la cual la conducción es segura (m).</summary>
        public float DribbleSafeDistance = 7f;

        /// <summary>Ruido de la utilidad con Decisions = 1 (se multiplica por la dificultad).</summary>
        public float DecisionNoise = 0.04f;
        /// <summary>Ventaja de mantener el balón conduciendo frente a pasar en igualdad de utilidad.</summary>
        public float DribbleBias = 0.002f;
        /// <summary>Castigo (en utilidad) por girar medio vuelta para conducir o proteger el balón: evita que el poseedor zigzaguee y dé vueltas al recibir.</summary>
        public float TurnPenalty = 0f;
        /// <summary>Decisiones seguidas en las que el poseedor mantiene el rumbo elegido al conducir o proteger el balón (solo un pase o un tiro lo interrumpen).</summary>
        public int OwnerCommitDecisions = 2;
        /// <summary>Distancia del rival más cercano que obliga a despejar dentro del área propia (m).</summary>
        public float ClearancePressure = 4f;

        /// <summary>Distancia ideal de un jugador de apoyo al que tiene el balón (m).</summary>
        public float SupportDistance = 11f;
        /// <summary>Jugadores que ofrecen apoyo al poseedor.</summary>
        public int SupportCount = 3;
        /// <summary>Puntos candidatos evaluados para desmarcarse.</summary>
        public int SupportCandidates = 6;
        /// <summary>Radio de los puntos candidatos alrededor del ancla (m).</summary>
        public float SupportSearchRadius = 8f;
        /// <summary>Distancia de la línea de fuera de juego a la que se queda el delantero (m).</summary>
        public float OffsideMargin = 1.5f;
        /// <summary>Distancia a la que un delantero se adelanta a su ancla para desmarcarse (m).</summary>
        public float RunAhead = 10f;

        /// <summary>Radio en el que un defensor marca a un rival (m).</summary>
        public float MarkRadius = 14f;
        /// <summary>Distancia a la que se sitúa el marcador respecto al rival, hacia su portería (m).</summary>
        public float MarkDistance = 2f;
        /// <summary>Peso del marcaje frente a la posición de formación.</summary>
        public float MarkBlend = 0.65f;
        /// <summary>Distancia a la que se activa la presión con presión media (m).</summary>
        public float PressTrigger = 14f;
        /// <summary>Distancia a la que el que presiona corre a máxima velocidad (m).</summary>
        public float PressSprintDistance = 9f;
        /// <summary>Distancia a la que el que presiona se planta entre el poseedor y su portería para contenerlo (m).</summary>
        public float ContainDistance = 2.0f;
        /// <summary>Distancia al poseedor desde la que el que contiene puede lanzarse a entrar (m).</summary>
        public float TackleEngageDistance = 2.6f;
        /// <summary>Probabilidad mínima de ganar el balón para que la IA entre (con presión media).</summary>
        public float TackleChanceThreshold = 0.30f;
        /// <summary>Cuánto baja ese umbral un equipo de presión máxima (y sube uno de presión mínima).</summary>
        public float TackleThresholdPressSpread = 0.10f;
        /// <summary>Distancia por detrás del que presiona a la que se sitúa el compañero que cubre (m).</summary>
        public float CoverDepth = 7f;
        /// <summary>Distancia máxima de un defensor al punto de cobertura para asignarle esa tarea (m).</summary>
        public float CoverRadius = 18f;

        /// <summary>Sin arquero (potrero): distancia mínima del cierre a su línea de gol al tapar el arco (m).</summary>
        public float GoalCoverMinDepth = 1.2f;
        /// <summary>Sin arquero: distancia máxima del cierre a su línea de gol (m).</summary>
        public float GoalCoverMaxDepth = 5f;
        /// <summary>Sin arquero: fracción de la distancia balón-arco a la que se adelanta el cierre.</summary>
        public float GoalCoverShare = 0.35f;

        /// <summary>Cambio de la altura de la línea por cada paso de mentalidad.</summary>
        public float MentalityLineStep = 0.12f;
        /// <summary>Cambio del riesgo por cada paso de mentalidad.</summary>
        public float MentalityRiskStep = 0.10f;
        /// <summary>Cambio del juego directo por cada paso de mentalidad.</summary>
        public float MentalityDirectnessStep = 0.10f;
        /// <summary>Cambio de la intensidad de presión por cada paso de presión.</summary>
        public float PressLevelStep = 0.30f;
        /// <summary>Minuto desde el que la IA adapta su táctica al marcador.</summary>
        public float AdaptStartMinute = 60f;
        /// <summary>Minuto desde el que la IA toma medidas extremas (todo al ataque o cerrar el partido).</summary>
        public float AdaptLateMinute = 80f;
        /// <summary>Cada cuántos segundos reales revisa la IA su táctica.</summary>
        public float AdaptInterval = 3f;

        /// <summary>Ventaja base de mantener el balón (esperar un desmarque) frente a jugarlo.</summary>
        public float HoldBias = 0.002f;
        /// <summary>Ventaja extra de mantener el balón de un equipo de posesión (Directness 0) respecto a uno directo (±).</summary>
        public float HoldStyleWeight = 0.012f;
        /// <summary>Distancia al rival más cercano a partir de la cual mantener el balón es seguro (m).</summary>
        public float HoldSafeDistance = 6f;
        /// <summary>Tiempo máximo seguido que la IA mantiene el balón sin jugarlo (s).</summary>
        public float HoldMaxTime = 1.5f;
        /// <summary>Distancia a la que se aleja del rival más cercano mientras protege el balón (m).</summary>
        public float HoldStep = 1.5f;

        /// <summary>Cuánto se desplaza el bloque hacia el balón (fracción del campo por cada fracción de posición del balón).</summary>
        public float BallShiftX = 0.35f;
        /// <summary>Cuánto se desplaza el bloque lateralmente hacia el balón (0..1).</summary>
        public float BallShiftY = 0.15f;
        /// <summary>Avance del bloque cuando el equipo tiene el balón (fracción del campo).</summary>
        public float PossessionAdvance = 0.12f;
        /// <summary>Retroceso del bloque cuando el equipo no tiene el balón (fracción del campo).</summary>
        public float DefendRetreat = 0.06f;
        /// <summary>Efecto de la altura de la línea del estilo sobre el bloque (fracción del campo).</summary>
        public float LineHeightEffect = 0.25f;
        /// <summary>Reducción de la distancia entre líneas con alta compactación.</summary>
        public float CompactnessEffect = 0.18f;

        /// <summary>Separación lateral entre los espacios que evalúa un delantero para desmarcarse (m).</summary>
        public float RunWidth = 8f;
        /// <summary>Preferencia por desmarcarse en línea recta frente a abrirse (m de espacio equivalentes).</summary>
        public float RunStraightBias = 1.5f;
        /// <summary>El arquero sale a achicar si un rival con balón está a esta distancia de su portería o menos (m).</summary>
        public float KeeperRushDistance = 15f;
        /// <summary>Distancia al poseedor a la que el arquero se planta al achicar (m).</summary>
        public float KeeperRushStop = 1.5f;
        /// <summary>Margen con el que un defensor cuenta como "entre el poseedor y la portería" (m).</summary>
        public float KeeperRushLaneWidth = 2.5f;

        /// <summary>Profundidad mínima del portero respecto a la línea de gol (m).</summary>
        public float KeeperMinDepth = 1.0f;
        /// <summary>Profundidad máxima del portero respecto a la línea de gol (m).</summary>
        public float KeeperMaxDepth = 3.5f;
        /// <summary>Cuánto se adelanta el portero por cada metro de distancia del balón.</summary>
        public float KeeperDepthPerMeter = 0.10f;

        /// <summary>Tiempo máximo de predicción del balón al buscar al interceptor (s).</summary>
        public float InterceptHorizon = 3f;
        /// <summary>Paso de la predicción del balón (s).</summary>
        public float InterceptStep = 0.1f;
        /// <summary>Segundos de ventaja que gana el receptor previsto de un pase al elegir interceptor.</summary>
        public float ReceiverBonus = 0.6f;
        /// <summary>Distancia entre compañeros a la que se reasigna el control del jugador humano (m).</summary>
        public float SwitchHysteresis = 4.5f;
        /// <summary>Tiempo mínimo entre cambios automáticos de jugador (s).</summary>
        public float SwitchCooldown = 1.2f;

        /// <summary>Si un jugador sin balón ya está a menos de esta distancia de su posición, se queda quieto (m).</summary>
        public float HoldRadius = 3f;
        /// <summary>Por debajo de esta distancia a su posición un jugador sin balón camina en lugar de trotar (m).</summary>
        public float WalkDistance = 9f;
        /// <summary>Fracción de la velocidad punta al caminar.</summary>
        public float WalkFraction = 0.32f;
        /// <summary>Velocidad de giro de un jugador parado hacia el balón, como fracción de su giro normal.</summary>
        public float IdleTurnFactor = 0.5f;
    }

    /// <summary>Parámetros de un nivel de dificultad. La dificultad sube por mejor decisión y ejecución, nunca por ventajas físicas.</summary>
    [Serializable]
    public sealed class DifficultyProfile
    {
        /// <summary>Nombre del nivel.</summary>
        public string Name = "Professional";
        /// <summary>Multiplicador del tiempo de reacción de la IA (mayor = más lenta).</summary>
        public float ReactionScale = 1f;
        /// <summary>Multiplicador del error de pase de la IA.</summary>
        public float PassErrorScale = 1f;
        /// <summary>Multiplicador del error de tiro de la IA.</summary>
        public float ShotErrorScale = 1f;
        /// <summary>Multiplicador de la presión de la IA al rival.</summary>
        public float PressScale = 1f;
        /// <summary>Multiplicador del ruido en las decisiones de la IA (mayor = peores decisiones).</summary>
        public float DecisionNoiseScale = 1f;
        /// <summary>Multiplicador del error de las acciones del jugador humano (menor = más ayuda).</summary>
        public float PlayerErrorScale = 1f;
        /// <summary>Fuerza del auto-apuntado de pases del jugador humano (0..1).</summary>
        public float PassAssist = 0.6f;
    }

    /// <summary>Configuración completa del motor de partido.</summary>
    [Serializable]
    public sealed class MatchConfig
    {
        /// <summary>Pasos de simulación por segundo.</summary>
        public int TickRate = 60;
        /// <summary>Campo.</summary>
        public PitchConfig Pitch = new PitchConfig();
        /// <summary>Balón.</summary>
        public BallConfig Ball = new BallConfig();
        /// <summary>Jugadores.</summary>
        public PlayerConfig Player = new PlayerConfig();
        /// <summary>Reglas.</summary>
        public RulesConfig Rules = new RulesConfig();
        /// <summary>Inteligencia artificial.</summary>
        public AiConfig Ai = new AiConfig();
        /// <summary>Informe del partido (notas de los jugadores).</summary>
        public ReportConfig Report = new ReportConfig();
        /// <summary>Roles individuales.</summary>
        public RoleConfig Roles = new RoleConfig();
        /// <summary>Director técnico en vivo.</summary>
        public CoachConfig Coach = new CoachConfig();
        /// <summary>Efecto del clima en el juego.</summary>
        public WeatherConfig Weather = new WeatherConfig();

        /// <summary>
        /// Ajusta esta configuración al clima del partido (césped mojado: el balón rueda más y los controles fallan más). Se llama una vez,
        /// con una configuración recién creada para ese partido.
        /// </summary>
        /// <param name="weather">Clima.</param>
        public void ApplyWeather(MatchWeather weather)
        {
            if (weather != MatchWeather.Rain)
            {
                return;
            }

            Ball.RollDeceleration *= Weather.RainRollScale;
            Ball.RollDrag *= Weather.RainRollScale;
            Player.MiscontrolChance *= Weather.RainMiscontrolScale;
            Player.HeavyTouchRate *= Weather.RainMiscontrolScale;
        }

        /// <summary>Niveles de dificultad: Principiante, Amateur, Profesional, Mundial, Leyenda.</summary>
        public DifficultyProfile[] Difficulties =
        {
            new DifficultyProfile { Name = "Beginner",     ReactionScale = 1.8f, PassErrorScale = 1.6f, ShotErrorScale = 1.6f, PressScale = 0.6f, DecisionNoiseScale = 2.0f, PlayerErrorScale = 0.5f, PassAssist = 0.9f },
            new DifficultyProfile { Name = "Amateur",      ReactionScale = 1.35f, PassErrorScale = 1.3f, ShotErrorScale = 1.3f, PressScale = 0.8f, DecisionNoiseScale = 1.5f, PlayerErrorScale = 0.7f, PassAssist = 0.75f },
            new DifficultyProfile { Name = "Professional", ReactionScale = 1.0f, PassErrorScale = 1.0f, ShotErrorScale = 1.0f, PressScale = 1.0f, DecisionNoiseScale = 1.0f, PlayerErrorScale = 0.85f, PassAssist = 0.6f },
            new DifficultyProfile { Name = "World Class",  ReactionScale = 0.8f, PassErrorScale = 0.8f, ShotErrorScale = 0.8f, PressScale = 1.15f, DecisionNoiseScale = 0.6f, PlayerErrorScale = 1.0f, PassAssist = 0.4f },
            new DifficultyProfile { Name = "Legend",       ReactionScale = 0.65f, PassErrorScale = 0.65f, ShotErrorScale = 0.65f, PressScale = 1.3f, DecisionNoiseScale = 0.3f, PlayerErrorScale = 1.0f, PassAssist = 0.2f }
        };

        /// <summary>Estilos de juego disponibles.</summary>
        public TeamStyle[] Styles =
        {
            new TeamStyle { Name = "Balanced",       LineHeight = 0.50f, PressIntensity = 0.50f, Width = 0.50f, Directness = 0.50f, Risk = 0.50f, Compactness = 0.50f },
            new TeamStyle { Name = "Possession",     LineHeight = 0.60f, PressIntensity = 0.55f, Width = 0.75f, Directness = 0.20f, Risk = 0.30f, Compactness = 0.50f,
                            MidfieldDuty = PlayerDuty.Playmaker },
            new TeamStyle { Name = "Counter Attack", LineHeight = 0.30f, PressIntensity = 0.30f, Width = 0.50f, Directness = 0.90f, Risk = 0.70f, Compactness = 0.60f,
                            StrikerDuty = PlayerDuty.Runner },
            new TeamStyle { Name = "High Press",     LineHeight = 0.80f, PressIntensity = 0.95f, Width = 0.60f, Directness = 0.60f, Risk = 0.60f, Compactness = 0.75f,
                            MidfieldDuty = PlayerDuty.BallWinner, FullbackDuty = PlayerDuty.WingBack },
            new TeamStyle { Name = "Defensive",      LineHeight = 0.20f, PressIntensity = 0.25f, Width = 0.40f, Directness = 0.70f, Risk = 0.30f, Compactness = 0.80f,
                            FullbackDuty = PlayerDuty.Defensive, StrikerDuty = PlayerDuty.TargetMan }
        };

        /// <summary>Duración de un paso de simulación (s).</summary>
        public float FixedDeltaTime() => 1f / TickRate;

        /// <summary>Calibración del xG en el potrero (arco chico sin arquero).</summary>
        private const float IndoorXgCalibration = 2.05f;

        /// <summary>Fracción del gasto de resistencia de la cancha grande que se usa en la cancha cerrada.</summary>
        public const float IndoorStaminaDrainFactor = 0.4f;

        /// <summary>
        /// Configuración del modo cancha cerrada (3 contra 3): campo de 40 × 20 m con paredes, porterías de fútbol sala
        /// (3 × 2 m), sin fuera de juego y con las distancias de la IA adaptadas al espacio reducido. Se guarda en
        /// <c>Data/balance/match_indoor.json</c>.
        /// </summary>
        public static MatchConfig CreateIndoor()
        {
            var c = new MatchConfig();
            PitchConfig p = c.Pitch;
            p.Length = 40f;
            p.Width = 20f;
            p.GoalWidth = 2.4f; // arco de potrero: sin arquero, más chico
            p.GoalHeight = 1.4f;
            p.PenaltyAreaDepth = 6f;
            p.PenaltyAreaHalfWidth = 7f;
            p.GoalAreaDepth = 0f;
            p.GoalAreaHalfWidth = 0f;
            p.PenaltySpotDistance = 6f;
            p.CenterCircleRadius = 3f;
            p.PlayerMargin = -0.4f;
            p.Walls = true;

            // Tres jugadores que corren todo el partido: gastan menos por segundo para terminar cansados, no agotados.
            c.Player.StaminaDrainSprint *= IndoorStaminaDrainFactor;
            c.Player.StaminaDrainJog *= IndoorStaminaDrainFactor;

            // Arco de 2,4 × 1,4 sin arquero: el modelo de xG de cancha grande sobrestima; se recalibra con 200 partidos.
            c.Report.XgCalibration = IndoorXgCalibration;

            RulesConfig r = c.Rules;
            r.Offside = false;
            r.SetPieceDistance = 5f;
            r.FreeKickDirectMaxDistance = 14f;

            AiConfig a = c.Ai;
            a.ShootMaxRange = 12f;
            a.ShootZoneDistance = 10f;
            a.XgDecay = 5f;
            a.ShotEagerness = 0.5f; // sin arquero el arco chico se busca tocando: menos remates forzados, más paredes
            a.ShotSpeedMax = 26f;
            a.PassMinDistance = 2.5f;
            a.PassMaxDistance = 28f;
            a.PassDistanceScale = 20f;
            a.LobPassDistance = 22f;
            a.CrossMinProgress = 0.97f;
            a.CrossWideDistance = 3f;
            a.DribbleLookahead = 4f;
            a.DribbleSafeDistance = 4f;
            a.ClearancePressure = 2.5f;
            a.OffsideMargin = 0f;
            a.RunAhead = 5f;
            a.MarkRadius = 9f;
            a.MarkDistance = 1.5f;
            a.PressTrigger = 9f;
            a.PressSprintDistance = 5f;
            a.CoverDepth = 4f;
            a.CoverRadius = 10f;
            a.KeeperMaxDepth = 3f;
            a.InterceptHorizon = 2f;
            a.HoldSafeDistance = 4f;
            a.HoldRadius = 1.5f;
            a.WalkDistance = 5f;
            a.SupportSearchRadius = 5f;
            return c;
        }
    }
}

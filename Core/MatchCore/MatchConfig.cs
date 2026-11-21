using System;

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
        /// <summary>Margen fuera de las líneas donde pueden moverse los jugadores.</summary>
        public float PlayerMargin = 2.5f;
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
        /// <summary>Velocidad punta con Pace = 1 (m/s).</summary>
        public float MinTopSpeed = 5.2f;
        /// <summary>Velocidad punta con Pace = 100 (m/s).</summary>
        public float MaxTopSpeed = 9.8f;
        /// <summary>Aceleración con Acceleration = 1 (m/s²).</summary>
        public float MinAcceleration = 3.8f;
        /// <summary>Aceleración con Acceleration = 100 (m/s²).</summary>
        public float MaxAcceleration = 7.4f;
        /// <summary>Frenada (m/s²).</summary>
        public float Deceleration = 9f;
        /// <summary>Velocidad de giro parado (rad/s).</summary>
        public float TurnRate = 10f;
        /// <summary>Cuánto reduce la velocidad la capacidad de girar.</summary>
        public float TurnSpeedPenalty = 0.09f;
        /// <summary>Fracción de la velocidad deseada cuando el jugador mira en dirección contraria.</summary>
        public float MisalignedSpeedFactor = 0.2f;
        /// <summary>Fracción de la velocidad punta al trotar.</summary>
        public float JogFraction = 0.6f;
        /// <summary>Gasto de resistencia por segundo a máxima velocidad.</summary>
        public float StaminaDrainSprint = 0.010f;
        /// <summary>Gasto de resistencia por segundo al trotar.</summary>
        public float StaminaDrainJog = 0.0012f;
        /// <summary>Recuperación de resistencia por segundo parado.</summary>
        public float StaminaRecovery = 0.003f;
        /// <summary>Fracción de la velocidad que se conserva con la resistencia agotada.</summary>
        public float StaminaSpeedFloor = 0.82f;
        /// <summary>Variación del gasto de resistencia según el atributo Stamina (±).</summary>
        public float StaminaAttributeSpread = 0.5f;
        /// <summary>Distancia mínima entre jugadores.</summary>
        public float SeparationDistance = 0.8f;
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
        /// <summary>Tiempo que se guarda un botón de golpeo pulsado justo antes de poder usarlo (s).</summary>
        public float HumanInputBuffer = 0.2f;
        /// <summary>Altura a la que el arquero sostiene el balón en las manos (m).</summary>
        public float KeeperHoldHeight = 1.0f;
        /// <summary>Distancia mínima del arquero con balón a su línea de gol (m): nunca la cruza con el balón en las manos.</summary>
        public float KeeperLineInset = 0.5f;
        /// <summary>Tras patear, el jugador no puede recuperar el balón durante este tiempo (s).</summary>
        public float KickCooldown = 0.5f;
        /// <summary>Tiempo tras controlar el balón antes de poder actuar (s).</summary>
        public float ControlDelay = 0.18f;
        /// <summary>Probabilidad por segundo de un mal toque a máxima velocidad con habilidad mínima.</summary>
        public float HeavyTouchRate = 0.45f;
        /// <summary>Velocidad extra del balón en un mal toque (m/s).</summary>
        public float HeavyTouchSpeed = 4.5f;

        /// <summary>Velocidad lateral del portero al lanzarse (m/s).</summary>
        public float KeeperDiveSpeed = 6.5f;
        /// <summary>Alcance del portero con Reflexes = 1 (m).</summary>
        public float KeeperBaseReach = 1.35f;
        /// <summary>Alcance adicional con Reflexes = 100 (m).</summary>
        public float KeeperExtraReach = 1.5f;
        /// <summary>Altura del centro de la esfera de alcance del portero (m).</summary>
        public float KeeperReachHeight = 1.0f;
        /// <summary>Probabilidad base de parada cuando el balón entra en el alcance.</summary>
        public float KeeperSaveBase = 0.85f;
        /// <summary>Variación de la parada según la habilidad del portero (±).</summary>
        public float KeeperSaveSkillSpread = 0.30f;
        /// <summary>Velocidad de tiro a partir de la cual la parada se vuelve más difícil (m/s).</summary>
        public float KeeperSaveSpeedRef = 14f;
        /// <summary>Penalización de la parada por cada m/s sobre la referencia.</summary>
        public float KeeperSaveSpeedPenalty = 0.045f;
        /// <summary>Velocidad máxima del balón que el portero puede atrapar (m/s).</summary>
        public float KeeperCatchMaxSpeed = 17f;
        /// <summary>Velocidad mínima del balón para considerarlo un tiro a portería (m/s).</summary>
        public float ShotMinSpeed = 11f;
    }

    /// <summary>Reglas, duración y árbitro.</summary>
    [Serializable]
    public sealed class RulesConfig
    {
        /// <summary>Duración real de cada parte (s).</summary>
        public float HalfDuration = 180f;
        /// <summary>Minutos de partido que representa cada parte.</summary>
        public float MinutesPerHalf = 45f;
        /// <summary>Espera antes de ejecutar un saque (s).</summary>
        public float RestartDelay = 1.4f;
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
        /// <summary>Multiplicador de falta de una entrada de pie por detrás.</summary>
        public float StandingBehindFoulMultiplier = 1.3f;
        /// <summary>Probabilidad base de falta en una entrada.</summary>
        public float FoulBase = 0.30f;
        /// <summary>Probabilidad base de tarjeta amarilla en una falta.</summary>
        public float YellowBase = 0.14f;
        /// <summary>Aumento de la amarilla según la agresividad (±).</summary>
        public float YellowAggressionSpread = 0.25f;
        /// <summary>Probabilidad de roja directa en una falta.</summary>
        public float RedBase = 0.008f;
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

        /// <summary>Distancia máxima a portería para considerar un tiro (m).</summary>
        public float ShootMaxRange = 30f;
        /// <summary>Escala de la probabilidad de gol esperada.</summary>
        public float XgScale = 0.75f;
        /// <summary>Distancia a la que la probabilidad cae a 1/e (m).</summary>
        public float XgDecay = 9.5f;
        /// <summary>Valor de la posesión tras un tiro fallado.</summary>
        public float ShotMissValue = 0.045f;
        /// <summary>Multiplicador de las ganas de tirar (juego de fútbol: más tiros que en la realidad por menos tiempo de partido).</summary>
        public float ShotEagerness = 2.2f;

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

        /// <summary>Profundidad mínima del portero respecto a la línea de gol (m).</summary>
        public float KeeperMinDepth = 1.0f;
        /// <summary>Profundidad máxima del portero respecto a la línea de gol (m).</summary>
        public float KeeperMaxDepth = 6.5f;
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

    /// <summary>Estilo de juego de un equipo.</summary>
    [Serializable]
    public sealed class TeamStyle
    {
        /// <summary>Nombre del estilo.</summary>
        public string Name = "Balanced";
        /// <summary>Altura de la línea defensiva (0 = muy atrás, 1 = muy adelantada).</summary>
        public float LineHeight = 0.5f;
        /// <summary>Intensidad de la presión (0..1).</summary>
        public float PressIntensity = 0.5f;
        /// <summary>Amplitud del equipo (0..1).</summary>
        public float Width = 0.5f;
        /// <summary>Preferencia por el juego directo hacia delante (0..1).</summary>
        public float Directness = 0.5f;
        /// <summary>Disposición a asumir riesgos en pases y tiros (0..1).</summary>
        public float Risk = 0.5f;
        /// <summary>Compactación entre líneas (0..1).</summary>
        public float Compactness = 0.5f;

        /// <summary>Crea una copia independiente (para modificarla durante un partido sin tocar la configuración).</summary>
        public TeamStyle Clone() => (TeamStyle)MemberwiseClone();
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
            new TeamStyle { Name = "Possession",     LineHeight = 0.60f, PressIntensity = 0.55f, Width = 0.75f, Directness = 0.20f, Risk = 0.30f, Compactness = 0.50f },
            new TeamStyle { Name = "Counter Attack", LineHeight = 0.30f, PressIntensity = 0.30f, Width = 0.50f, Directness = 0.90f, Risk = 0.70f, Compactness = 0.60f },
            new TeamStyle { Name = "High Press",     LineHeight = 0.80f, PressIntensity = 0.95f, Width = 0.60f, Directness = 0.60f, Risk = 0.60f, Compactness = 0.75f },
            new TeamStyle { Name = "Defensive",      LineHeight = 0.20f, PressIntensity = 0.25f, Width = 0.40f, Directness = 0.70f, Risk = 0.30f, Compactness = 0.80f }
        };

        /// <summary>Duración de un paso de simulación (s).</summary>
        public float FixedDeltaTime() => 1f / TickRate;
    }
}

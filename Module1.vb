' #####################################################################
' # L Minett 2020 - Snake                                             #
' # Ideas to implement:                                               #
' # 1) Do not paint borders if wrap is on.                            #
' # 2) Add a bomb that randomly appears and can kill the snake        #
' #    to make things challenging, bomb appears in path of snake      #
' # 3) As levels increase, so do obstacles and starting speed         #
' # 4) Add different symbols such as allow wrap, or pass through      #
' #    barriers, etc (temporarily)                                    #
' # 5) Add multi-player, where they can eat each others food, etc     #
' # 6) Game could speed up faster the bigger the apple score          #
' # 7) Slow snake down as health decreases? May make things worse     #
' # 8) Animated obstacles? Enemies?                                   #
' # 9) Special object that appears after x number of top apples       #
' #    which is worth a huge number of points, but lasts a few secs   #
' # 10) Life hearts can appear                                        #
' # 11) Energy pods can be collected and eaten when most needed       #
' #     they arrive very rarely and a button such as + activates      #
' # 12) Difficulty levels altering starting health, screen size,etc   #
' # 13) Music and sfx                                                 #
' # 14) Change decay system to link to game speed rather than         #
' #     independent timer?                                            #
' # 15) Add keypad support and allow diagnal travel (against spirit   #
' #     of the original game, but so is border wrap!                  #
' # 16) Persist some game settings to project strings to get saved    #
' #     with the rest of the program automatically when changed       #
' # 17) Include a high score table list/array of (high score struct)  #
' #                                                                   #
' #####################################################################

' ####################################################### NOTES #######################################################
' # There are many features still to be programmed. Goto: View -> Other Windows -> Task List to bring up the project's task list
' #
' # for multi-player, possibly have a 'player' structure, comprising of game and snake or go fully O-O
' # Apple positions are generated from a hashset of coordinates to avoid problems finding non-collision space in game
' # can use MS Word Insert Symbol to easily peruse Consolas font for Unicode symbols

Imports System.Console
Imports System.Text

Module Module1
#Region "Declarations"
#Region "Game Parameters"

    Const StartingLives As Short = 6 ' default starting lives
    Dim StartingPos As New CoOrdinate With {.x = 5, .y = 5} ' starting position for snake
    Const SnakeColour As ConsoleColor = ConsoleColor.White
    Const SnakeBody As Integer = 9608 ' unicode character for square block
    Const StartingSnakeLength As Short = 5 ' starting length
    Const AllowWrap As Boolean = True ' it true, snake can wrap around screen
    Const StartingSpeed As Integer = 120 ' starting speed (ms) that snake will move - classed as a 'TICK'
    Dim startingConsoleSize As New CoOrdinate With {.x = 100, .y = 50}
    Const GameSpeedIncrease As Single = 0.95 ' % by which to increase game speed each time apple is eaten

    ' Health is calculated by dT = dT * sc^as (dT is metabolism time decay, sc is scaling factor and as is apple tail increase value
    ' it is exponential decrease so a tail of 400 doesn't become impossible.  Play with scaling factor to affect how quickly the decay increases
    Const StartingMetabolismDeltaT As Short = 1000 ' how many ms to decrease health by 1% (as you get bigger, you need more calories) 
    Const MetabolismCeiling As Short = 80 ' this is the highest the snake's metabolism will reach (in ms). E.g. 100 = 10 health units per second
    Const MetabolismScalingFactor As Single = 0.98 ' The scaling fator 
    Const StartingHealth As Short = 80 ' as a %

    ' Apples
    Dim AppleMode As AppleModes = AppleModes.KeepHighest
    Const AppleShape As Integer = &H25CF ' circle shape
    Const AppleDecayedShape As Integer = &H25CC ' dotted circle
    Const AppleDecayStart As Single = 0.5 ' as a %age of its lifespan. e.g 0.25 decays after 25% of its time on screen. 0 turns decay off
    Const AppleDecayReduction As Single = 0.5 ' if apple decayed, what % of original values is it worth. E.g. 0.5 is 50% (always rounded up to nearest integer)

    ' create pot of apples: name, colour on screen, points worth, tail length added, time available (in ms) and helath increase
    Dim Apples() As StrucApple = {
        New StrucApple("Red", ConsoleColor.Red, 10, 1, 18000, 10),
        New StrucApple("Green", ConsoleColor.Green, 20, 2, 12000, 15),
        New StrucApple("Yellow", ConsoleColor.Yellow, 30, 3, 10000, 20),
        New StrucApple("Pink", ConsoleColor.Magenta, 50, 5, 7000, 30)
    }
#End Region

#Region "Globals"
    Dim Timer_Game As New Stopwatch ' controls game speed
    Dim Timer_Apple As New Stopwatch ' determines when apples expire
    Dim Timer_Health As New Stopwatch ' when to decrease health by 1%
    Dim GameApple As StrucGameApple ' stores details of the apple on the screen
    Dim Game As StrucGame ' stores all details relating to the game
    Dim Snake As StrucSnake ' stores details relating to snake (or multiple if multi-player)
    Dim Screen As StrucScreen ' stores details of console size, etc
    Dim ApplePositions As New HashSet(Of CoOrdinate) ' maintains a list of all places apples can go

#End Region

#Region "Data Structure Definitions"
    Enum Direction
        Up
        Down
        Left
        Right
    End Enum
    Enum CollisionReason
        None
        Border
        Obstacle ' when implemented
        Apple
        Self
    End Enum
    Enum AppleModes
        TrueRandom ' any apple can be picked, irrespective of value
        Incremental ' must gain previous apple to get next one, or resets back to top
        StatisticalRandom ' the further down the list of apples, the less likely to be picked
        KeepHighest ' player works up to highest and keeps it until lose a life
    End Enum
    Structure StrucApple ' defines possible apples
        Dim Name As String ' Apple name
        Dim Colour As ConsoleColor
        Dim Points As Short ' how many points to award
        Dim TTL As Short ' apple's time to live in ms
        Dim TailAdd As Short ' how many units to add to the tail once swallowed
        Dim Health As Short ' percentage of health to increase (use 100 to restore to full health)

        Public Sub New(n As String, clr As ConsoleColor, pts As Short, tail As Short, lifetime As Short, Hlth As Short)
            ' constructor built to enable easy adding of apple types
            Name = n
            Colour = clr
            Points = pts
            TTL = lifetime
            TailAdd = tail
            Health = Hlth
        End Sub
    End Structure
    Structure StrucGameApple ' relates to the current screen apple
        Dim AppleDetails As StrucApple ' can be assigned one of the apple types
        Dim Location As CoOrdinate ' where this is on the screen
        Dim AppleType As Short ' specifies the index of the current apple being used
        Dim Decay As Boolean ' if True, apple has started to decay (if and when implemented) - will save needless half-life timer checks
    End Structure

    Structure CoOrdinate
        Dim x As Short
        Dim y As Short

        ''' <summary>
        ''' Compare two coordinates to see if they match
        ''' </summary>
        ''' <param name="c1"></param>
        ''' <param name="c2"></param>
        ''' <returns>Boolean</returns>
        Public Shared Operator =(c1 As CoOrdinate, c2 As CoOrdinate) As Boolean ' enables the compiler to perform a check if two coordinates are the same
            If c1.x = c2.x And c1.y = c2.y Then
                Return True
            Else
                Return False
            End If
        End Operator
        Public Shared Operator <>(c1 As CoOrdinate, c2 As CoOrdinate) As Boolean ' needed by compiler to allow for overriding of = operator
            Return Not (c1 = c2)
        End Operator
    End Structure
    Structure StrucSnake
        Dim Position As CoOrdinate
        Dim Direction As Direction
        Dim Trail As Queue(Of CoOrdinate) ' maintains queue of snake's tail positions
        Dim Tail As Short ' length of tail 
        Dim Lives As Short ' Number of lives left
        Dim Health As Short ' health of snake in %
        Dim HealthDeltaT As Single ' no. ms health will decrease
        Dim Colour As ConsoleColor ' what colour should the snake be
        Dim BodyShape As Integer ' unicode character for body

    End Structure

    Structure StrucGame
        Dim Wrap As Boolean ' wrap around screen
        Dim Score As Integer
        Dim Level As Short ' not yet implemented
        Dim Speed As Integer
    End Structure
    Structure StrucScreen
        Dim Width As Short
        Dim Height As Int16
        Dim BorderTopLeft As CoOrdinate
        Dim BorderBottomRight As CoOrdinate
    End Structure

#End Region
#End Region
    Sub Main()
        CursorVisible = False

        IntroScreen()

    End Sub
#Region "Initialisation"
    ''' <summary>
    ''' Set up for a new game
    ''' </summary>
    Sub Initialise() ' initialise variables, etc.
        Console.OutputEncoding = Encoding.UTF8 ' switch to different encoding to support game glyphs

        With Game
            .Level = 1
            .Score = 0
            .Wrap = AllowWrap
            .Speed = StartingSpeed
        End With

        ' set console dimensions
        With Screen
            .Width = If(Console.LargestWindowWidth < startingConsoleSize.x, LargestWindowWidth - 1, startingConsoleSize.x) ' is desired size bigger than actual allowed?
            .Height = If(Console.LargestWindowHeight < startingConsoleSize.y, LargestWindowHeight - 1, startingConsoleSize.y)
            .BorderTopLeft = New CoOrdinate With {.x = 0, .y = 1}
            .BorderBottomRight = New CoOrdinate With {.x = Screen.Width - 1, .y = Screen.Height - 1}
        End With

        Console.SetWindowSize(Screen.Width, Screen.Height) ' change console size to match requirements
        Console.SetBufferSize(Screen.Width + 1, Screen.Height + 1) ' change console memory screen buffer accordingly
        Console.SetWindowPosition(0, 0) ' set buffer position to match screen size

        ' set snake lives here as cannot do it in reset level
        Snake.Lives = StartingLives
        ResetLevel()

    End Sub
    ''' <summary>
    ''' Will initialise a new set of coordinates where apples can go.  Call before each new level
    ''' </summary>
    Sub InitialiseApplePositions()
        ' the MoveSnake and DrawObstacles will populate and depopulate this set as they are drawn
        ' populate with all space in-between border

        For x As Integer = Screen.BorderTopLeft.x + 1 To Screen.BorderBottomRight.x - 1
            For y As Integer = Screen.BorderTopLeft.y + 1 To Screen.BorderBottomRight.y - 1
                ApplePositions.Add(New CoOrdinate With {.x = x, .y = y}) ' create coordinate and add to set
            Next
        Next
    End Sub

    ''' <summary>
    ''' Call to reset snake and level back to starting position (e.g. lose life, health, etc)
    ''' </summary>
    Sub ResetLevel()
        ' called when starting a new game, or whenever the player loses a life
        InitialiseApplePositions() ' generate fresh set of apple positions as tail is reset



        'ToDo: Start snake in random direction
        With Snake ' restore/reset default snake except for lives
            .Direction = Direction.Down
            .Position = StartingPos
            .Colour = SnakeColour
            .BodyShape = SnakeBody
            .Tail = StartingSnakeLength
            .Health = StartingHealth
            .HealthDeltaT = StartingMetabolismDeltaT
            .Trail = New Queue(Of CoOrdinate) ' initialise the queue
        End With

        Game.Speed = StartingSpeed ' reset speed
        DrawScreen()
        NewApple()
        DrawSnake()

    End Sub
#End Region

#Region "Game Screen Modes"
    Sub IntroScreen()
        ' ToDo: code intro screen and ability to change some game parameters
        ' display an intro screen and options, etc

        PlayGame() ' start a new game
    End Sub
    ''' <summary>
    ''' This is the game loop that will run until game over
    ''' </summary>
    Sub PlayGame()
        ' this is the main game engine
        Dim HandleLives As Boolean = False ' if set true, user lost a life
        Dim TempDeltaT As Single ' holds the new health delta t for checking (storing saves calculating several times)

        Initialise() ' will reset values, draw the screen and generate a new apple

        Timer_Game.Start()
        Timer_Health.Start()
        My.Computer.Audio.Play(My.Resources.Always_Moving_Forward__2m_, playMode:=AudioPlayMode.BackgroundLoop) ' audio file is kept in project resources
        Do
            Do ' this code will execute continuously until snake or apple timer needs servicing

                If Console.KeyAvailable Then
                    Select Case Console.ReadKey(True).Key
                        Case ConsoleKey.UpArrow
                            Snake.Direction = Direction.Up
                        Case ConsoleKey.DownArrow
                            Snake.Direction = Direction.Down
                        Case ConsoleKey.LeftArrow
                            Snake.Direction = Direction.Left
                        Case ConsoleKey.RightArrow
                            Snake.Direction = Direction.Right
                        Case ConsoleKey.Q
                            Exit Sub
                        'ToDo: Remove these debug keys below
                        Case ConsoleKey.Add
                            Snake.Health += 2
                        Case ConsoleKey.Subtract
                            Snake.Health -= 2
                    End Select
                End If

                ' exit loop if time to advance snake, decrease health or destroy apple
            Loop Until Timer_Game.ElapsedMilliseconds >= Game.Speed _
                Or Timer_Apple.ElapsedMilliseconds >= GameApple.AppleDetails.TTL _
                Or Timer_Health.ElapsedMilliseconds >= Snake.HealthDeltaT

            ' Check health
            If Timer_Health.ElapsedMilliseconds >= Snake.HealthDeltaT Then
                Snake.Health -= 1
                Timer_Health.Restart()
            End If
            ' Once health checks done, check if it needs to trigger a life loss
            If Snake.Health <= 0 Then
                Snake.Lives -= 1
                HandleLives = True
            End If

            'Apple timer check
            If Timer_Apple.ElapsedMilliseconds >= GameApple.AppleDetails.TTL Then
                ' do something
                Timer_Apple.Stop()
                DestroyApple()
            ElseIf Not GameApple.Decay AndAlso Timer_Apple.ElapsedMilliseconds >= (GameApple.AppleDetails.TTL - (GameApple.AppleDetails.TTL * AppleDecayStart)) Then
                ' if not already decayed, check if apple's time on screen has reached the allocated decay time
                DecayApple()
            End If

            'Check for and handle collisions
            If Timer_Game.ElapsedMilliseconds >= Game.Speed Then
                MoveSnake() ' call sub to move current position 
                Select Case Collision()
                    Case CollisionReason.None
                        ' safe to update snake position
                        DrawSnake()

                    Case CollisionReason.Apple
                        Game.Score += GameApple.AppleDetails.Points
                        Snake.Tail += GameApple.AppleDetails.TailAdd
                        Snake.Health = If(Snake.Health + GameApple.AppleDetails.Health > 100, 100, Snake.Health + GameApple.AppleDetails.Health) ' health cannot go above 100
                        DrawSnake()
                        NewApple(False) ' no need to cover over apple as snake body will do this
                        Game.Speed = Math.Ceiling(Game.Speed * GameSpeedIncrease) ' increase speed by given %age
                        If MetabolismCeiling <> Snake.HealthDeltaT Then ' ignore if already at ceiling
                            TempDeltaT = Math.Ceiling(Snake.HealthDeltaT * (MetabolismScalingFactor ^ GameApple.AppleDetails.TailAdd))
                            Snake.HealthDeltaT = If(TempDeltaT > MetabolismCeiling, TempDeltaT, MetabolismCeiling)
                        End If

                    Case CollisionReason.Self
                        Snake.Lives -= 1
                        HandleLives = True

                    Case CollisionReason.Border
                        ' only triggered if wrap is turned off
                        Snake.Lives -= 1
                        HandleLives = True
                End Select
                Timer_Game.Restart() ' restart timer
            End If
            DrawHUD()

            If HandleLives Then
                If Snake.Lives = 0 Then
                    GameOver()
                Else
                    ResetLevel()
                End If
                HandleLives = False
            End If

        Loop Until Snake.Lives = 0

    End Sub
    Sub GameOver()
        'ToDo: Handle game over graphics

    End Sub
    Sub ShowDeathReason(Reason As CollisionReason)
        ' ToDo: display the reason
        Select Case Reason
            Case CollisionReason.Border

            Case CollisionReason.Obstacle

            Case CollisionReason.Self

        End Select
    End Sub
#End Region

#Region "Game Mechanics"
    ''' <summary>
    ''' This will move current position according to current direction
    ''' </summary>
    Sub MoveSnake()
        ' will check if wrap is on and move snake to avoid hitting border
        'Dim co As CoOrdinate = Snake.Position ' to reduce code clutter
        Select Case Snake.Direction
            Case Direction.Up
                Snake.Position.y = If(Game.Wrap And Snake.Position.y - 1 = Screen.BorderTopLeft.y, Screen.BorderBottomRight.y - 1, Snake.Position.y - 1) ' if hit top border, cycle to bottom
            Case Direction.Down
                Snake.Position.y = If(Game.Wrap And Snake.Position.y + 1 = Screen.BorderBottomRight.y, Screen.BorderTopLeft.y + 1, Snake.Position.y + 1) ' same for bottom border
            Case Direction.Left
                Snake.Position.x = If(Game.Wrap And Snake.Position.x - 1 = Screen.BorderTopLeft.x, Screen.BorderBottomRight.x - 1, Snake.Position.x - 1) ' and for left border 
            Case Direction.Right
                Snake.Position.x = If(Game.Wrap And Snake.Position.x + 1 = Screen.BorderBottomRight.x, Screen.BorderTopLeft.x + 1, Snake.Position.x + 1) ' and for right border
        End Select

    End Sub
    ''' <summary>
    ''' Check to see if current position will result in a collision and returns reason, or None for no collision
    ''' </summary>
    ''' <returns>Type of collision</returns>
    Function Collision() As CollisionReason
        ' need to check if current position is going to hit a wall, self, obstacle, etc
        ' if so, return reason, otherwise if no collision, return none

        ' check apple
        If Snake.Position = GameApple.Location Then
            Return CollisionReason.Apple
        End If

        ' check border
        If Snake.Position.x = Screen.BorderTopLeft.x Or Snake.Position.x = Screen.BorderBottomRight.x Or
                Snake.Position.y = Screen.BorderTopLeft.y Or Snake.Position.y = Screen.BorderBottomRight.y Then
            Return CollisionReason.Border
        End If

        'check tail collision
        If Snake.Trail.Contains(Snake.Position) Then
            Return CollisionReason.Self
        End If

        'ToDo: Add obstacle collision

        Return CollisionReason.None
    End Function
#End Region

#Region "Game Graphics/Display"
    ''' <summary>
    ''' Draws the border around the screen
    ''' </summary>
    Sub DrawBorder()

        Dim ct As CoOrdinate = Screen.BorderTopLeft
        Dim cb As CoOrdinate = Screen.BorderBottomRight

        ForegroundColor = ConsoleColor.White
        Console.SetCursorPosition(ct.x, ct.y)
        Console.WriteLine(StrDup(cb.x, ChrW(&H2550))) ' solid bar top 

        Console.SetCursorPosition(ct.x, cb.y)
        Console.WriteLine(StrDup(cb.x, ChrW(&H2550))) ' solid bar bottom
        For y As Integer = ct.y To cb.y ' draw vertical lines
            SetCursorPosition(ct.x, y)
            Console.Write(ChrW(&H2551))
            SetCursorPosition(cb.x, y)
            Console.Write(ChrW(&H2551))
        Next
        ' draw corners
        SetCursorPosition(ct.x, ct.y)
        Console.Write(ChrW(&H2554))
        SetCursorPosition(cb.x, ct.y)
        Console.Write(ChrW(&H2557))
        SetCursorPosition(ct.x, cb.y)
        Console.Write(ChrW(&H255A))
        SetCursorPosition(cb.x, cb.y)
        Console.Write(ChrW(&H255D))


    End Sub
    ''' <summary>
    ''' Call to redraw the screen
    ''' </summary>
    Sub DrawScreen()
        Console.Clear()
        DrawBorder()
        DrawHUD()
        DrawSnake()
        'ToDo: draw obstacles
    End Sub
    ''' <summary>
    ''' Call to update 'head up display': Lives, score, etc.
    ''' </summary>
    Sub DrawHUD()
        ' Draw score
        ForegroundColor = ConsoleColor.Cyan
        SetCursorPosition(0, 0)
        Write($"Score:{Game.Score.ToString.PadLeft(6, " ")}")

        ' Draw lives
        ForegroundColor = ConsoleColor.Green
        SetCursorPosition(15, 0)
        Write("lives:")
        ForegroundColor = ConsoleColor.Red
        SetCursorPosition(21, 0)
        Write(StrDup(If(Snake.Lives > 5, 5, Snake.Lives), ChrW(&H2665))) ' write lives out as hearts, if more than 5, only show 5 at a time
        If Snake.Lives > 5 Then Write("+") ' to indicate more than 5 lives

        ' Draw health
        ' much more faffy as need to write text at same time as health bar background 
        SetCursorPosition(30, 0)
        Console.WriteLine("Health:")
        Dim HealthBarText As String = (Snake.Health.ToString & "%   ").PadLeft(10, " ")
        SetCursorPosition(38, 0)
        ForegroundColor = ConsoleColor.Black
        For counter As Integer = 0 To Len(HealthBarText) - 1
            If Math.Ceiling(Snake.Health / 10) - 1 >= counter Then ' adjust by -1 as starting at 0
                BackgroundColor = If(Snake.Health >= 50, ConsoleColor.Green, If(Snake.Health > 10, ConsoleColor.Yellow, ConsoleColor.Red))
            Else
                BackgroundColor = ConsoleColor.Gray
            End If
            Write(HealthBarText(counter))
        Next
        BackgroundColor = ConsoleColor.Black

        'ToDo: Draw Level (when implemented)

    End Sub
    ''' <summary>
    ''' Display the snake on the screen at current position and modify trail queue as needed
    ''' </summary>
    Sub DrawSnake()
        ' this sub adds current postion to queue and removes trace of tail (if needed)
        ' if body and colour can change dynamically in the game, modify this code to iterate entire trail queue

        ' first draw head
        SetCursorPosition(Snake.Position.x, Snake.Position.y) ' current snake position
        ForegroundColor = Snake.Colour
        Write(ChrW(SnakeBody))
        Snake.Trail.Enqueue(Snake.Position) ' add current position to snake body
        ApplePositions.Remove(Snake.Position) ' remove this position from Apple positions

        ' now check if tail needs to be erased
        While Snake.Trail.Count > Snake.Tail 'if trail queue is longer than tail size, remove from queue.  Will also allow adjustment if tail penalised
            SetCursorPosition(Snake.Trail.Peek.x, Snake.Trail.Peek.y) ' Position of earliest drawn snake trail
            ApplePositions.Add(Snake.Trail.Dequeue()) ' remove position from trail and add it back to possible apple positions
            Write(" ") ' cover snake tail
        End While


    End Sub
    ''' <summary>
    ''' Will look at array of obstacles and draw these on the screen
    ''' </summary>
    Sub DrawObstacles()
        'ToDo: Not yet implemented
    End Sub

#End Region

#Region "Manage Apples"
    ''' <summary>
    ''' Generates a new apple onto the screen and initialises gameapple with details
    ''' </summary>
    Sub NewApple(Optional Restart As Boolean = True) ' Restart TRUE indicates player restarting due to loss of life (default value)
        'apple modes are documented on enum declarations

        Dim AppleSelection As Short ' will be assigned the apple to use
        Select Case AppleMode
            Case AppleModes.TrueRandom
                AppleSelection = New Random().Next(0, Apples.Count - 1) ' from 0 to number of types of apple
            Case AppleModes.StatisticalRandom
                'ToDo: Implement statistical random for apple selection
                AppleSelection = New Random().Next(0, Apples.Count - 1) ' this is a true random for now
            Case AppleModes.Incremental
                AppleSelection = If(Not (Restart) And GameApple.AppleType < (Apples.Count - 1), GameApple.AppleType + 1, 0)
            Case AppleModes.KeepHighest
                AppleSelection = If(Not (Restart), If(GameApple.AppleType < (Apples.Count - 1), GameApple.AppleType + 1, (Apples.Count - 1)), 0)
        End Select

        ' now apple has been selected, set details
        With GameApple
            .AppleDetails = Apples(AppleSelection)
            .AppleType = AppleSelection
            .Location = ApplePositions(New Random().Next(0, ApplePositions.Count)) ' pick a random position from apple positions
            .Decay = False
        End With

        DisplayApple()
    End Sub
    ''' <summary>
    ''' Draws an apple on the screen from the references given in GameApple
    ''' </summary>
    Sub DisplayApple()
        ' display apple
        ForegroundColor = GameApple.AppleDetails.Colour
        SetCursorPosition(GameApple.Location.x, GameApple.Location.y)
        Write(ChrW(AppleShape)) ' circle shape
        Timer_Apple.Restart() ' start new apple timer

    End Sub
    Sub DestroyApple()
        ' this is called when the user fails to collect in time
        ForegroundColor = GameApple.AppleDetails.Colour
        SetCursorPosition(GameApple.Location.x, GameApple.Location.y)
        Write(" ")
        Game.Score = If(Game.Score - GameApple.AppleDetails.Points > 0, Game.Score - GameApple.AppleDetails.Points, 0) ' score cannot be negative
        NewApple() ' generate a new apple
    End Sub
    Sub DecayApple()
        ' sets parameters for decay (50% of values)
        If Not (GameApple.Decay) Then ' don't bother alredy decayed
            With GameApple.AppleDetails
                .Health = Math.Ceiling(.Health / 2)
                .Points = Math.Ceiling(.Points / 2)
                .TailAdd = Math.Ceiling(.TailAdd / 2)
            End With
            GameApple.Decay = True
            ForegroundColor = GameApple.AppleDetails.Colour
            SetCursorPosition(GameApple.Location.x, GameApple.Location.y)
            Write(ChrW(AppleDecayedShape))
        End If
    End Sub
#End Region
End Module

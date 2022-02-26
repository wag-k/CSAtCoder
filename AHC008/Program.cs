using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace AtCoder.AHC008
{
    public class Program
    {
        static void Main(string[] args)
        {
            // 初期位置を記したボード
            var initBoard = new Board();

            using var cin = new Scanner();
            int numPets = cin.Int();
            //var incrementPos = new Pos(1,1); // Boardに壁を作るため、１個インクリメントして座標を登録する。
            var initPetsPositions = new (int, int, int)[numPets];
            //Console.WriteLine("#PetsNumDone");
            var pets = new Pet[numPets];
            for(int n = 0; n < numPets; ++n)
            {
                initPetsPositions[n] = cin.Int3();
                var initPos = new Pos(initPetsPositions[n].Item1, initPetsPositions[n].Item2);
                pets[n] = PetFactory.Create((PetType)initPetsPositions[n].Item3, initPos, initBoard);
            }
            PetFactory.ResetPetIndex();

            //Console.WriteLine("#PetsInputDone");
            var numHumans = cin.Int();
            var initHumansPositions = new (int, int)[numHumans];
            var humans = new Human[numHumans];
            for(int m = 0; m < numHumans; ++m){
                initHumansPositions[m] = cin.Int2();
                WriteLine($"#{initHumansPositions[m].Item1}, {initHumansPositions[m].Item2}");

                var initPos = new Pos(initHumansPositions[m].Item1, initHumansPositions[m].Item2);
                humans[m] = HumanFactory.Create(initPos, initBoard);
            }
            HumanFactory.ResetHumanIndex();

            WriteLine("#HumansInputDone");

            var simulator = new TerritorySimulator(humans, pets, initBoard);
            simulator.Simulate();
        }

        public static void WriteLine(string msg){
            Console.WriteLine(msg);
            Console.Out.Flush();
        }
    }

    public struct Pos{
        public int X{get; set;}
        public int Y{get; set;}

        public Pos(int x, int y){
            X = x;
            Y = y;
        }

        public static Pos operator+ (Pos posLeft, Pos posRight){
            return new Pos(posLeft.X+posRight.X, posLeft.Y + posRight.Y);
        }

        public static Pos operator- (Pos posLeft, Pos posRight){
            return new Pos(posLeft.X-posRight.X, posLeft.Y - posRight.Y);
        }

        
        public static bool operator== (Pos posLeft, Pos posRight){
            return posLeft.X == posRight.X && (posLeft.Y == posRight.Y);
        }
        public static bool operator!= (Pos posLeft, Pos posRight){
            return !(posLeft == posRight);
        }

        public static Pos ErrorPos{get {return new Pos(-100, -100);}}
    }	

    public enum PetType{
        None = 0,
        Cow,
        Pig,
        Rabbit,
        Dog, 
        Cat,
    }

    public enum Direction{
        None = 0,
        Up,
        Down,
        Left,
        Right
    }

    public enum FloorType{
        None = 0,
        Wall,
        Pet,
        Human,
        PetAndHuman,
    }

    public class TerritorySimulator{

        // public Human[] Humans { get; set;} 
        // public Pet[] Pets {get; set;}
        public static int TotalTurn { get{return 300;}}

        public Scene CurrentScene {get; set;}

        CaptureStrategy Strategy {get; set;}

        public TerritorySimulator(Human[] humans, Pet[] pets, Board initBoard)
        {
            // Humans = humans;
            // Pets = pets;

            var initScene = new Scene()
            {
                Turn = 0,
                Humans = humans,
                Pets = pets  ,
                Board = initBoard
            };


            var currentPetsPositions = new int[pets.Length];
            CurrentScene = new Scene(initScene);

            // Sceneを作ってからじゃないとNREする
            Strategy = new CaptureStrategy(this);

        }
        
        public void Simulate(){
            for(int turnCnt = 0; turnCnt < TotalTurn ; ++ turnCnt){
                // Program.WriteLine($"# IsCurrentSceneNull: {CurrentScene == null}");
                Strategy.SetCurrentTurn(turnCnt, CurrentScene);
                var movements  = DecideHumansMovement();
                // SendMovementsOrder
                Program.WriteLine(movements);
                // Receive PetsMovements
                GetPetsMove(CurrentScene);

                //var currentScore = CurrentScene.CalcTotalScore();
                //Program.WriteLine($"#Turn: {CurrentScene.Turn}");
                // Program.WriteLine($"#Turn: {CurrentScene.Turn}, Score: {currentScore}");
                // CurrentScene = new Scene(CurrentScene);
                
                CurrentScene.Refresh();
                /*
                if(8 == CurrentScene.Turn%10){
                    CurrentScene.Board.Show();
                }
                */
                CurrentScene.Turn += 1;
            }
        }
        
        public string DecideHumansMovement()
        {
            var outSB = new StringBuilder();                
            for(int m = 0; m < CurrentScene.Humans.Length; ++m){
                // Program.WriteLine($"# Human: {m}");
                var human = CurrentScene.Humans[m];
                var humanAction = Strategy.DecideNextAction(human, CurrentScene);
                human.Action(humanAction);
                outSB.Append(humanAction);
                
                // CurrentScene.Board.Show();
            } 
            return outSB.ToString();
        }

        public void GetPetsMove(in Scene scene)
        {
            using var cin = new Scanner();
            var petActions = cin.ArrayString(scene.Pets.Length);
            for(int n = 0; n < scene.Pets.Length; ++n)
            {
                scene.Pets[n].Action(petActions[n]);
            }
        }
    }

    public class CaptureStrategy
    {
        public TerritorySimulator Simulator {get; set;}
        /// <summary>
        /// 現在捕まえたいペット
        /// </summary>
        /// <value></value>
        public int CurrentTargetPetIndex{get; set;}

        /// <summary>
        /// 現在捕まえたいペット
        /// </summary>
        /// <value></value>
        public Pet CurrentTargetPet{get => Simulator.CurrentScene.Pets[CurrentTargetPetIndex];}



        /// <summary>
        /// 残りの捕獲対象
        /// Petsのインデックスで管理 
        /// </summary>
        public List<int> TargetPets {get; set;}

        public int CurrenteTurn {
            get => _currentTurn;
            private set {
                this._currentTurn = value;
            }
        }
        int _currentTurn;

        /// <summary>
        /// 人間がどこに向かっているか
        /// </summary>
        /// <value></value>
        Pos[] CapturingPositions{ get; set;}

        public void SetCurrentTurn(int turn, Scene scene)
        {
            CurrenteTurn =turn;
            TargetPets = SearchTargetPets(scene);
            CurrentTargetPetIndex = SelectNextTarget();
        }

        public delegate int TargetSelectionHandler();

        public CaptureStrategy()
        {
            CurrentTargetPetIndex = -1;
            CapturingPositions = new Pos[100];
        }
        public CaptureStrategy(TerritorySimulator simulator)
        {
            CurrentTargetPetIndex = -1;
            CapturingPositions = (
                from index in Enumerable.Range(0,  simulator.CurrentScene.Humans.Length)
                select Pos.ErrorPos
            ).ToArray();
            Program.WriteLine($"# CapturingPositions.Length: {CapturingPositions.Length}");
            
            Simulator = simulator;
        }

        public string DecideNextAction(Human human, Scene scene){
            if(!TargetPets.Contains(CurrentTargetPetIndex))
            {
                Program.WriteLine("#SearchNextPets: Begin");

                // CurrentTargetPetIndex = SelectNextTarget();
                if(CurrentTargetPetIndex == -1)
                {
                    return "."; // 終了
                }
            }
            (var direcion, var dist) = ApproachToTarget(human, scene);


            if(dist == 0){
                return DecideMakeWall(human, scene);
            }
            var manhattanDistance = Board.CalcManhattanDistance(CurrentTargetPet.Pos, human.Pos);
            //if(manhattanDistance == 3 || manhattanDistance == 4){
            if(manhattanDistance == 3){
                return DecideMakeWall(human, scene);
            }
            /*
            if(dist == 1)
            {
                var leaveDirection = LeaveFromPet(human, scene);
                return MovingObject.DirectionToMoveCommandDict[direcion];
            }
            */
            else {
                return MovingObject.DirectionToMoveCommandDict[direcion];
            }
        }

        /// <summary>
        /// 目標に近づく
        /// BFSで距離をメモしておけばよい
        /// 距離が減るほうに進む。
        /// アプローチ方向を返す
        /// 目的地に近づく方向と目的地までの距離を返す。
        
        /// distは目的地(理想の壁つくりのための場所)までの距離
        /// </summary> 
        public (Direction direction, int dist) ApproachToTarget(Human human, Scene scene)
        {
            // Parentをメモ
            var distBoard = new (Pos parentPos, int dist, Direction direction)[32,32];
            var maxDist = 1000;
            for(int x = 0; x < 32; ++x)
            {
                for (int y =0 ; y < 32; ++y)
                {
                    distBoard[x, y] = (Pos.ErrorPos, maxDist, Direction.None);
                }
            }

            var capturingPos = GetCapturingPositions();
            // var capturingPos = GetCapturingPositionsToConstructionSite();
            Program.WriteLine($"# HumanNo: {human.Index} humanPos: ({human.Pos.X}, {human.Pos.Y}), capturingPos: ({capturingPos.X}, {capturingPos.Y})");
            CapturingPositions[human.Index] = capturingPos;
            
            var idealWallPositoins = new List<Pos>();
            for(var wallIndex = 0; wallIndex < _idealWallVectors.Length; ++wallIndex)
            {
                var idealWallPos = CurrentTargetPet.Pos + _idealWallVectors[wallIndex];
                if(!Board.IsInsideOfBoard(idealWallPos)) {
                    continue;
                }
                idealWallPositoins.Add(idealWallPos);
            }
            // 場所がなければどこにもいかない。
            if(capturingPos == Pos.ErrorPos)
            {
                return (Direction.None, maxDist);
            }
            else if(idealWallPositoins.Contains(human.Pos))
            {
                // 建設予定地にいたら逃げる。
                return (LeaveFromPet(human, scene), 0);
            } 
            /*
            else if(human.Pos == capturingPos){
                // 建設予定地にいたら逃げる。
                return (LeaveFromPet(human, scene), 0);
            }
            */

            /// <summary>
            /// 
            /// </summary>
            /// <param name="parentPos"></param>
            /// <param name="dist"></param>
            /// <param name="direction">parentPos から弦座標に行くために必要な方向</param>
            /// <typeparam name="parentPos"></typeparam>
            /// <typeparam name="dist"></typeparam>
            /// <typeparam name="direction"></typeparam>
            /// <returns></returns>
            var searchPosQueue = new Queue<(Pos parentPos, int dist)>();
            distBoard[human.Pos.X, human.Pos.Y] = (human.Pos, 0, Direction.None);
            searchPosQueue.Enqueue((human.Pos, 0));
            // Program.WriteLine("#ApproachToTarget: BeginSearch");
            while(searchPosQueue.Count > 0)
            {
                (Pos searchPos, int dist) = searchPosQueue.Dequeue();
                
                if(searchPos == capturingPos){
                    break;
                }

                // Queueに入れる順番に優先度をつける。
                var orderBuffetList = new List<(int sortStandard, (Pos parentPos, int dist) posDist)>();
                for(int directionIndex = 0; directionIndex < MovingObject.Directions.Length; directionIndex++)
                {
                    var direction = MovingObject.Directions[directionIndex];
                    var nextPos = MovingObject.Shift(searchPos, direction);

                    // 一度訪れたことのあるマスのErrorPosではない。
                    if(
                        ( MovingObject.CheckMovable(nextPos, scene.Board)) 
                        && (distBoard[nextPos.X, nextPos.Y].parentPos == Pos.ErrorPos))
                    {
                        var nextDist = dist + 1;
                        distBoard[nextPos.X, nextPos.Y] = (searchPos, nextDist, direction);
                        // ターゲットと調べている地点の2点が作る四角形の面積。これが大きいほうを優先して探索したい
                        var differencePos = capturingPos - nextPos;
                        int targetDistSquare = Math.Abs(differencePos.X*differencePos.Y);
                        orderBuffetList.Add((targetDistSquare, (nextPos, nextDist)));
                    }
                    ;
                }
                if(orderBuffetList.Count == 0){
                    continue;
                }
                // SortしてEnqueue
                orderBuffetList.Sort((left, right )=>{
                    return left.sortStandard.CompareTo(right.sortStandard);
                });
                orderBuffetList.Reverse();
                for(int orderIndex = 0 ; orderIndex < orderBuffetList.Count; ++orderIndex)
                {
                    // Program.WriteLine($"# square: {orderBuffetList[orderIndex].sortStandard}");
                    var posDist = orderBuffetList[orderIndex].posDist;
                    searchPosQueue.Enqueue(posDist);
                }  

            }
            // Program.WriteLine("#ApproachToTarget: SearchDone");
            var currentPos = distBoard[capturingPos.X, capturingPos.Y];
            var distToCapturingPos = distBoard[capturingPos.X, capturingPos.Y].dist;
            if(distToCapturingPos == maxDist)
            {
                return (Direction.None, maxDist);
            }
            while(1 < currentPos.dist)
            {
                var parentPos = currentPos.parentPos;
                //Program.WriteLine($"#ParentPos: ({parentPos.X}, {parentPos.Y}), dist: {currentPos.dist}");
                currentPos = distBoard[parentPos.X, parentPos.Y];
            }
            return (currentPos.direction, distToCapturingPos);
        }

        public Direction LeaveFromPet(Human human, Scene scene){
            Program.WriteLine($"#LeaveFromPet: human:{human.Index}");
            
            var petToHumanVec = human.Pos - scene.Pets[CurrentTargetPetIndex].Pos;
            bool isCheckX = false;
            if(petToHumanVec.X > 0)
            {
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Right), scene.Board))
                {
                    return Direction.Right;
                }
                isCheckX = true;
            } else if(petToHumanVec.X < 0)
            {
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Left), scene.Board))
                {
                    return Direction.Left;
                }
                isCheckX = true;
            }

            
            bool isCheckY = false;
            if(petToHumanVec.Y > 0)
            {
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Down), scene.Board))
                {
                    return Direction.Down;
                }
                isCheckY = true;
            } else if(petToHumanVec.Y < 0)
            {
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Up), scene.Board))
                {
                    return Direction.Up;
                }
                isCheckY = true;
            }

            if(!isCheckX)
            {
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Down), scene.Board))
                {
                    return Direction.Down;
                }
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Up), scene.Board))
                {
                    return Direction.Up;
                }
            }

            if(!isCheckY)
            {
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Right), scene.Board))
                {
                    return Direction.Right;
                }
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, Direction.Left), scene.Board))
                {
                    return Direction.Left;
                }
            }

            
            for(int directionIndex = 1; directionIndex < MovingObject.Directions.Length; directionIndex++)
            {
                var direction = MovingObject.Directions[directionIndex];
                Program.WriteLine($"# Try Lave Direction: {direction}");
                if(MovingObject.CheckMovable(MovingObject.Shift(human.Pos, direction), scene.Board))
                {
                    return direction;
                }
            }
            return Direction.None;
        }

        public string DecideMakeWall(Human human, Scene scene)
        {
            // Program.WriteLine($"# Decide Make Wall: Begin");
            
            var targetPetPos = Simulator.CurrentScene.Pets[CurrentTargetPetIndex].Pos;
            var board = scene.Board;
            for(int directionIndex = 0; directionIndex < MovingObject.Directions.Length; directionIndex++)

            {
                var direction = MovingObject.Directions[directionIndex];
                if(direction == Direction.None)
                {
                    continue;
                }
                var constructionSite = MovingObject.Shift(human.Pos, direction);
                if(!Board.IsInsideOfBoard(constructionSite)
                    || !Human.CheckWallMakable(constructionSite, board))
                {
                    continue;
                }

                for (int index = 0; index < WallSiteVectors.Length; ++index)
                {
                    //Program.WriteLine($"# Serch Making Wall Pos. Index: {index}");

                    var idealWallPos = targetPetPos + WallSiteVectors[index];
                    if((constructionSite == idealWallPos)){
                        // Program.WriteLine($"# FoundGoodConstructionSite: ({idealWallPos.X}, {idealWallPos.Y}), Direction: {direction}");
                        return MovingObject.DirectionToMakingWallCommandDict[direction];
                    }else {
                        continue;
                    }

                }
            }
            return ".";
        }

        /// <summary>
        /// 捕獲するための待機場所
        /// 重複しないようにする。
        /// 候補地がほかになければ重複OK
        /// </summary>
        /// <returns>capturingPos</returns>
        public Pos GetCapturingPositions()
        {
            var targetPetPos = Simulator.CurrentScene.Pets[CurrentTargetPetIndex].Pos;
            // Program.WriteLine($"#TargetPet: {CurrentTargetPet}, Pos: ({targetPetPos.X}, {targetPetPos.Y})");

            var board = Simulator.CurrentScene.Board;
            var capturingPos = Pos.ErrorPos;
            for (int index = 0; index < _candidateRelativeVectors.Length; ++index)
            {
                var idealWallPos = targetPetPos + _idealWallVectors[index];
                if(!Board.IsInsideOfBoard(idealWallPos)) {
                    continue;
                }
                var constructionSite = board[idealWallPos];
                var canditateCapturingPosition = targetPetPos + _candidateRelativeVectors[index];
                if(
                    constructionSite != FloorType.Wall 
                    && Human.CheckWallMakable(idealWallPos, board)
                    && board[canditateCapturingPosition] != FloorType.Wall
                    && board[canditateCapturingPosition] != FloorType.Human
                ){
                    capturingPos = canditateCapturingPosition;
                    if(!CapturingPositions.Contains(capturingPos))
                    {
                        return capturingPos;
                    }
                }
            }
            return capturingPos;
        }

        /// <summary>
        /// 捕獲するための待機場所
        /// 壁建設地の隣ではなく、建設予定地そのものに向かう
        /// 重複しないようにする。
        /// 候補地がほかになければ重複OK
        /// </summary>
        /// <returns>capturingPos</returns>
        public Pos GetCapturingPositionsToConstructionSite()
        {
            var targetPetPos = Simulator.CurrentScene.Pets[CurrentTargetPetIndex].Pos;
            // Program.WriteLine($"#TargetPet: {CurrentTargetPet}, Pos: ({targetPetPos.X}, {targetPetPos.Y})");

            var board = Simulator.CurrentScene.Board;
            var capturingPos = Pos.ErrorPos;
            for (int index = 0; index < _candidateRelativeVectors.Length; ++index)
            {
                var idealWallPos = targetPetPos + _idealWallVectors[index];
                if(!Board.IsInsideOfBoard(idealWallPos)) {
                    continue;
                }
                var constructionSiteFloorType = board[idealWallPos];
                if(
                    constructionSiteFloorType != FloorType.Wall 
                    && Human.CheckWallMakable(idealWallPos, board)
                ){
                    capturingPos = idealWallPos;
                    if(!CapturingPositions.Contains(capturingPos))
                    {
                        return capturingPos;
                    }
                }
            }
            return capturingPos;
        }

        public static Pos[] WallSiteVectors{
            get => _idealWallVectors;
        }
        /// <summary>
        /// 理想的な壁建設予定地のベクトル
        /// </summary>
        /// <value></value>
        static Pos[] _idealWallVectors = new Pos[8]{
            new Pos(-1, -1),
            new Pos(-2, 0),
            new Pos(0, -2),
            new Pos(-1, 1),
            new Pos(1, -1),
            new Pos(0, 2),
            new Pos(2, 0),
            new Pos(1, 1),
        };

        /// <summary>
        /// ２マス先まで許容した壁建設予定地のベクトル
        /// </summary>
        /// <value></value>
        static Pos[] _extendedWallVectors = new Pos[20]{
            new Pos(-1, -1),
            new Pos(-2, 0),
            new Pos(0, -2),
            new Pos(-1, 1),
            new Pos(1, -1),
            new Pos(0, 2),
            new Pos(2, 0),
            new Pos(1, 1),
            new Pos(-3, 0),
            new Pos(-2, -1),
            new Pos(-2, 1),
            new Pos(-1, -2),
            new Pos(-1, 2),
            new Pos(0, -3),
            new Pos(0, 3),
            new Pos(1, -2),
            new Pos(1, 2),
            new Pos(2, -1),
            new Pos(2, 1),
            new Pos(3, 0),
        };

        /// <summary>
        /// 壁建設予定地に壁を建設するのに理想的なPos。
        /// Indexは_idealWallVectorsに対応
        /// </summary>
        /// <value></value>
        static Pos[] _candidateRelativeVectors = new Pos[8]{
            new Pos(-2, -1),
            new Pos(-2, 1),
            new Pos(-1, -2),
            new Pos(-1, 2),
            new Pos(1, -2),
            new Pos(1, 2),
            new Pos(2, -1),
            new Pos(2, 1),
        };

        /// <summary>
        /// 壁建設予定地に壁を建設可能な相対座標。
        /// Indexは_idealWallVectorsに対応
        /// </summary>
        /// <value></value>
        static Pos[] _wallMakableRelativeVectors = new Pos[12]{
            new Pos(-3, 0),
            new Pos(-2, -1),
            new Pos(-2, 1),
            new Pos(-1, -2),
            new Pos(-1, 2),
            new Pos(0, -3),
            new Pos(0, 3),
            new Pos(1, -2),
            new Pos(1, 2),
            new Pos(2, -1),
            new Pos(2, 1),
            new Pos(3,0),
        };

        static Pos[] _extendedWallMakableRelativeVectors = new Pos[28]{
            new Pos(-4, 0),
            new Pos(-3, -1),
            new Pos(-3, 0),
            new Pos(-3, 1),
            new Pos(-2, -2),
            new Pos(-2, -1),
            new Pos(-2, 1),
            new Pos(-2, 2),
            new Pos(-1, -3),
            new Pos(-1, -2),
            new Pos(-1, 2),
            new Pos(-1, 3),
            new Pos(0, -4),
            new Pos(0, -3),
            new Pos(0, 3),
            new Pos(0, 4),
            new Pos(1, -3),
            new Pos(1, -2),
            new Pos(1, 2),
            new Pos(1, 3),
            new Pos(2, -2),
            new Pos(2, -1),
            new Pos(2, 1),
            new Pos(2, 2),
            new Pos(3, -1),
            new Pos(3, 0),
            new Pos(3, 1),
            new Pos(4, 0),
        };
        /// <summary>
        /// 見つからない場合は-1
        /// </summary>
        /// <returns></returns>
        public int SelectNextTarget()
        {
            return SelectNextTarget(this.SelectTargetByNearestManhattan);
        }

        public int SelectNextTarget(TargetSelectionHandler targetSelectionHandler)
        {
            return targetSelectionHandler();
        }


        public int SelectTargetByNearestManhattan()
        {
            // Program.WriteLine($"# IsSimulatorNull: {Simulator == null}");
            // Program.WriteLine($"# IsSimulator.CurrentSceneNull: {Simulator.CurrentScene == null}");
            // Program.WriteLine($"# IsSimulator.CurrentScene.HumansNull: {Simulator.CurrentScene.Humans == null}");

            var humansCog = CalcCoG(Simulator.CurrentScene.Humans);
            int nearestIndex = -1;
            int nearestDist = 100;
            for (int index = 0; index < TargetPets.Count; ++index)
            {
                var currentDist = Board.CalcManhattanDistance(humansCog, Simulator.CurrentScene.Pets[TargetPets[index]].Pos);
                if(currentDist < nearestDist)
                {
                    nearestDist = currentDist;
                    nearestIndex = index;
                }
            }
            return nearestIndex;
        }

        public static Pos CalcCoG(IMovingObject[] movingObjects)
        {
            Pos sumPos = new Pos(0,0);
            for(int index = 0; index < movingObjects.Length; ++index)
            {
                sumPos += movingObjects[index].Pos;
            }
            var cogPos = new Pos(sumPos.X/movingObjects.Length, sumPos.Y/movingObjects.Length);
            return cogPos;
        }

        /// <summary>
        /// 残りの捕獲対象を創作
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static List<int> SearchTargetPets(Scene scene)
        {
            var targetPetsSet = new HashSet<int>();

            UnionFindTreeWithUnionSize boardGroup = Scene.MakeBoardGroup(scene);
            var board = scene.Board;

            // そのペットは人間の居場所に存在するか？
            var groupScores = new double[board.Width*board.Height];

            for(int humanIndex = 0; humanIndex < scene.Humans.Length; ++humanIndex)
            {
                var human = scene.Humans[humanIndex];
                var humanPosIndex = board.GetBoardIndex(human.Pos);
                var humanGroup = boardGroup.FindRoot(humanPosIndex);

                // すでに探索ずみの場合はメモしておいたスコアを使う。
                // スコアは必ず0より大きい
                if(0 < groupScores[boardGroup.FindRoot(humanPosIndex)])
                {
                    continue;
                }

                var currentPetsSet = new HashSet<int>();
                for (int petIndex = 0; petIndex < scene.Pets.Length; ++petIndex)
                {
                    var pet = scene.Pets[petIndex];
                    var petPosIndex = board.GetBoardIndex(pet.Pos);
                    var petGroup =  boardGroup.FindRoot(petPosIndex);
                    if(humanGroup == petGroup)
                    {
                        currentPetsSet.Add(pet.Index);
                    }
                }
                var score = 1;
                groupScores[boardGroup.FindRoot(humanPosIndex)] = score;

                targetPetsSet.UnionWith(currentPetsSet);
            }
            var targetPetsList = targetPetsSet.ToList();
            targetPetsList.Sort();
            return targetPetsList;
        }
    }

    public class Scene{
        public int Turn {get; set;}
        public Human[] Humans { get; set;} 
        public Pet[] Pets {get; set;}
        public Board Board{get; set;}
        public UnionFindTreeWithUnionSize BoardGroup { get; private set;}
        public int Score{ get; set;}

        public Scene(){}

        /// <summary>
        /// スコアは引き継がない。
        /// </summary>
        /// <param name="originalScene"></param>

        public Scene(Scene originalScene)
        {
            Turn = originalScene.Turn;
            Board = new Board(originalScene.Board);
            Humans =
            ( 
                from human in originalScene.Humans
                select HumanFactory.Create(human.Pos, Board)
            ).ToArray();
            HumanFactory.ResetHumanIndex();
            Pets = 
            (
                from pet in originalScene.Pets
                select PetFactory.Create(pet.PetType, pet.Pos, Board)
            ).ToArray();
            PetFactory.ResetPetIndex();
        }

        /// <summary>
        /// Refresh Scene Board
        /// </summary>
        public void Refresh()
        {
            int boardSize = Board.Width * Board.Height;
            for (int index =  0; index < boardSize; ++index)
            {
                if(Board[index] != FloorType.Wall){
                    Board[index] = FloorType.None;
                }
            }
            for (int m = 0; m < Humans.Length; ++m)
            {
                var human = Humans[m];
                Board[human.Pos] = FloorType.Human;
            }
            // Program.WriteLine($"#Human[0] Pos: ({Humans[0].Pos.X},{Humans[0].Pos.Y})");
            for (int n = 0; n < Pets.Length; ++n)
            {
                var pet = Pets[n];
                Board[pet.Pos] = FloorType.Pet;
            }
        }

        /// <summary>
        /// スコア計算の高速化は肝になる気がする。
        /// 自分の領土にペットがいないことが最優先
        /// 深さをメモしてBFSすればよい
        /// BFS & Union-Find Grouping
        /// </summary>
        /// <param name="human"></param>
        /// <param name="board"></param>
        /// <returns></returns>
        public int CalcTotalScore(){
            if(BoardGroup == null) 
            {
                this.BoardGroup = MakeBoardGroup(this);
            }

            double totalScore = 0;
            // そのグループに属していたら何点か？
            // ここ高速化できる。
            var groupScores = new double[Board.Width*Board.Height];

            for(int humanIndex = 0; humanIndex < Humans.Length; ++humanIndex)
            {
                var human = Humans[humanIndex];
                var humanPosIndex = Board.GetBoardIndex(human.Pos);
                var humanGroup = BoardGroup.FindRoot(humanPosIndex);

                // すでに探索ずみの場合はメモしておいたスコアを使う。
                // スコアは必ず0より大きい
                if(0 < groupScores[BoardGroup.FindRoot(humanPosIndex)])
                {
                    totalScore += groupScores[BoardGroup.FindRoot(humanPosIndex)];
                    continue;
                }

                var petNum = 0; // ｈumanのエリアに何匹Petがいるか？
                for (int petIndex = 0; petIndex < Pets.Length; ++petIndex)
                {
                    var pet = Pets[petIndex];
                    var petPosIndex = Board.GetBoardIndex(pet.Pos);
                    var petGroup =  BoardGroup.FindRoot(petPosIndex);
                    if(humanGroup == petGroup)
                    {
                        petNum += 1;
                    }
                }
                var groupSize = BoardGroup.FindGroupSize(humanGroup);
                var score = groupSize*Math.Pow(2, -petNum);
                groupScores[BoardGroup.FindRoot(humanPosIndex)] = score;
                totalScore += score;
            }
            var coef = 100000000;
            totalScore = coef*totalScore/900/Humans.Length;
            return (int)Math.Floor(totalScore);
        }

        /// <summary>
        /// Humanのいるエリアでペットのますが１であることはありえないから、
        /// もし、ペットのユニオンサイズが１ならそこに人はいない。
        /// </summary>
        /// <param name="scene"></param>
        public static UnionFindTreeWithUnionSize MakeBoardGroup(Scene scene)
        {
            var board = scene.Board;
            var boardGroup = new UnionFindTreeWithUnionSize(board.Height*board.Width);
            for(int humanIndex = 0; humanIndex < scene.Humans.Length; ++humanIndex)
            {
                var initHumanPos = scene.Humans[humanIndex].Pos;                
                // BFS
                var queue = new Queue<Pos>();
                queue.Enqueue(initHumanPos);
                while(queue.Count > 0)
                {
                    var currentPos = queue.Dequeue();
                    var currentPosIndex = board.GetBoardIndex(currentPos);
                    for(int directionIndex = 0; directionIndex < MovingObject.Directions.Length; directionIndex ++)
                    {
                        var direction = MovingObject.Directions[directionIndex];
                        var targetPos = MovingObject.Shift(currentPos, direction);
                        // 一度訪れたことのあるマスのUniteSizeは１よりい大きい
                        if(
                            ( MovingObject.CheckMovable(targetPos, board)) 
                            && (boardGroup.FindGroupSize(board.GetBoardIndex(targetPos)) <= 1))
                        {
                            queue.Enqueue(targetPos);
                            boardGroup.Unite(currentPosIndex, board.GetBoardIndex(targetPos));
                        }
                    }        
                }
            }
            return boardGroup;
        }


    }

    public class UnionFindTreeWithUnionSize : UnionFindTree
    {
        int[] _unionSize;
        public UnionFindTreeWithUnionSize(int treeSize) : base (treeSize)
        {
            _unionSize = new int[treeSize];
            for (int index = 0; index < treeSize ; ++index)
            {
                _unionSize[index] = 1;
            }
        }

        public override void Unite(int x, int y)
        {
            x = FindRoot(x);
            y = FindRoot(y);

            if(x == y){return;}

            var newUnionSize =  _unionSize[x] + _unionSize[y];

            if(_rank[x] < _rank[y])
            {
                _parent[x] = y;

            }
            else
            {
                _parent[y] = x;
                if(_rank[x] == _rank[y])
                {
                    _rank[x]++;
                }
            }
            _unionSize[FindRoot(x)] = newUnionSize;
        }
        
        public int FindGroupSize(int searchIndex)
        {
            var root = FindRoot(searchIndex);
            return _unionSize[root];
        }
    }

    public interface IMovingObject{
        FloorType FloorType{get;}
        int Index { get ; set;}
        Pos Pos{get; set;}
        Board Board {get; set;}
        IEnumerable<Pos> GetMovableArea();
        bool Action(string actionCommand);

    }
    public static class MovingObject{
        public static Pos Shift(Pos pos, Direction direction)
        {
            var shiftVector = GetShiftVector(direction);
            return pos+shiftVector;
        }
        public static Pos GetShiftVector(Direction direction){
            switch (direction)
            {
                case Direction.None:
                    return new Pos(0, 0);
                case Direction.Up:
                    return new Pos(-1, 0);
                case Direction.Down:
                    return new Pos(1, 0);
                case Direction.Left:
                    return new Pos(0, -1);
                case Direction.Right:
                    return new Pos(0, 1);
                default:
                    throw new ArgumentException($"{nameof(GetShiftVector)}Error");
            }
        }

        
        public static bool CheckMovable(Pos pos, Board board){
            if((pos.X < 1) || (pos.Y < 1) || (board.Width-1 <= pos.X) || (board.Height-1 <= pos.Y))
            {
                return false;
            }
            return board[pos] != FloorType.Wall;
        }
        
        
        public static bool StandardMove(IMovingObject movingObject, Direction direction)
        {
            Pos nextPos = movingObject.Pos + MovingObject.GetShiftVector(direction);
            return StandardCheckAndMove(movingObject, nextPos);
        }

        public static bool StandardCheckAndMove(IMovingObject movingObject, Pos nextPos){
            var isMovable = MovingObject.CheckMovable(nextPos, movingObject.Board);
            if(isMovable){
                movingObject.Pos = nextPos;
            }
            if(movingObject.Board[nextPos] != FloorType.Pet)
            {
                movingObject.Board[nextPos] = movingObject.FloorType;
            }
            return isMovable;
        }

        public static Dictionary<Direction, string> DirectionToMoveCommandDict {
            get {return _directionToMoveCommandDict;}
        }
        static Dictionary<Direction, string> _directionToMoveCommandDict = new Dictionary<Direction, string>{
            {Direction.None, "."},
            {Direction.Up, "U"},
            {Direction.Down, "D"},
            {Direction.Left, "L"},
            {Direction.Right, "R"},
        };
        
        public static Dictionary<Direction, string> DirectionToMakingWallCommandDict {
            get {return _directionToMakingWallCommandDict;}
        }
        static Dictionary<Direction, string> _directionToMakingWallCommandDict = new Dictionary<Direction, string>{
            {Direction.None, "."},
            {Direction.Up, "u"},
            {Direction.Down, "d"},
            {Direction.Left, "l"},
            {Direction.Right, "r"},
        };

        public static Direction[] Directions{
            get => _directions;
        }
        static Direction[] _directions = new Direction[5]{
            Direction.None,
            Direction.Up,
            Direction.Down,
            Direction.Left,
            Direction.Right,
        };
    }

    public static class HumanFactory
    {
        static int _humanIndex = 0;
        public static Human Create(Pos pos, Board board)
        {
            board[pos] = FloorType.Human;

            var createdHuman = new Human(){
                Index = _humanIndex,
                Pos = pos,
                Board = board
            };
            _humanIndex++;
            Program.WriteLine($"#CreatedHuman: {createdHuman.Index}");

            return createdHuman;
        }

        public static void ResetHumanIndex()
        {
            _humanIndex = 0;
        }
    }

    public class Human : IMovingObject{
        public  FloorType FloorType{get => FloorType.Human;}
        public int Index { get; set;}
        public Pos Pos{get; set;}
        public Board Board{get; set; }
        public IEnumerable<Pos> GetMovableArea(){
            for(int directionIndex = 0; directionIndex < MovingObject.Directions.Length; directionIndex ++)
            {
                var direction = MovingObject.Directions[directionIndex];
                var checkPos = Pos + MovingObject.GetShiftVector(direction);
                if(MovingObject.CheckMovable(Pos, Board)){
                    yield return checkPos;
                }
            }
        }
        public bool Action(string actionCommand){
            switch (actionCommand)
            {
                case ".":
                    return Move(Direction.None);
                case "u":
                    return MakeWall(Direction.Up);
                case "d":
                    return MakeWall(Direction.Down);
                case "l":
                    return MakeWall(Direction.Left);
                case "r":
                    return MakeWall(Direction.Right);
                case "U":
                    return Move(Direction.Up);
                case "D":
                    return Move(Direction.Down);
                case "L":
                    return Move(Direction.Left);
                case "R":
                    return Move(Direction.Right);
                default:
                    return false;
            }
        }

        bool Move(Direction direction)
        {
            return MovingObject.StandardMove(this, direction);
        }

        bool MakeWall(Direction direction){
            var constructionSite = MovingObject.Shift(this.Pos,direction);
            return CheckAndMakeWall(constructionSite);
        }

        bool CheckAndMakeWall(Pos constructionSite){
            var isWallMakable = CheckWallMakable(constructionSite, Board);
            if(isWallMakable){
                Board[constructionSite] = FloorType.Wall;
            }
            return isWallMakable;
        }

        public static bool CheckWallMakable(Pos constructionSite, Board board)
        {
            for(int directionIndex = 0; directionIndex < MovingObject.Directions.Length; directionIndex++)
            {
                var direction = MovingObject.Directions[directionIndex];
                try{
                    var shiftedPos = MovingObject.Shift(constructionSite, direction);
                    var floorType = board[shiftedPos];
                    if(direction == Direction.None){
                        if(floorType != FloorType.None){
                            return false;
                        }
                        continue;
                    }else{
                        // 隣にペットがいたらだめ。
                        if(floorType==FloorType.Pet){
                            return false;
                        }
                        // 人間を捕まえるのはだめ。
                        if(floorType==FloorType.Human)
                        {
                            int wallNumAroundHuman = 0;
                            for(int directionHumanIndex = 0; directionHumanIndex < MovingObject.Directions.Length; directionHumanIndex++)
                            {
                                var directionHuman = MovingObject.Directions[directionHumanIndex];
                                var posAroundHuman = MovingObject.Shift(shiftedPos, directionHuman);
                                if(board[posAroundHuman] == FloorType.Wall){
                                    wallNumAroundHuman += 1;
                                }
                            }
                            if(wallNumAroundHuman == 3){
                                return false;
                            }
                        }
                        continue;
                    }
                }
                catch(IndexOutOfRangeException){
                    continue;
                }
            }
            return true;
        }
    }

    public static class PetFactory
    {
        static int _petIndex = 0;
        public static Pet Create(PetType petType, Pos pos, Board board)
        {
            board[pos] = FloorType.Pet;
            Pet createdPet;
            switch (petType)
            {
                case PetType.Cow:
                    createdPet = new Cow(){
                        Index = _petIndex,
                        Pos = pos,
                        Board = board
                    };
                    break;
                case PetType.Pig:
                    createdPet = new Pig(){
                        Index = _petIndex,
                        Pos = pos,
                        Board = board
                    };
                    break;
                case PetType.Rabbit:
                    createdPet = new Rabbit(){
                        Index = _petIndex,
                        Pos = pos,
                        Board = board
                    };
                    break;
                case PetType.Dog:
                    createdPet = new Dog(){
                        Index = _petIndex,
                        Pos = pos,
                        Board = board
                    };
                    break;
                case PetType.Cat:
                    createdPet = new Cat(){
                        Index = _petIndex,
                        Pos = pos,
                        Board = board
                    };
                    break;
                default:
                    throw new ArgumentException("CreateError");
            }
            _petIndex++;
            return createdPet;
        }
        public static void ResetPetIndex()
        {
            _petIndex = 0;
        }
    }

    public abstract class Pet : IMovingObject{
        public  FloorType FloorType{get => FloorType.Pet;}


        public int Index { get; set;}
        public Pos Pos{get; set;}
        public Board Board{get; set; }
        public abstract PetType PetType {get;}

        /// <summary>
        /// How many times does a pet move in 1 turn 
        /// </summary>
        /// <value></value>
        public abstract int TotalMoveCnt {get;}

        public virtual IEnumerable<Pos> GetMovableArea()
        {
            IEnumerable<Pos> checkPositions = new Pos[]{Pos};
            for(int moveCnt = 0; moveCnt < TotalMoveCnt-1; ++moveCnt){
                checkPositions = SearchMovablePositions(checkPositions);
            }
            return SearchMovablePositions(checkPositions);
        }

        IEnumerable<Pos> SearchMovablePositions(IEnumerable<Pos> checkPositions){
            foreach (var checkPosition in checkPositions)
            {
                for(int directionIndex = 0; directionIndex < MovingObject.Directions.Length; directionIndex++)
                {
                    var direction = MovingObject.Directions[directionIndex];
                    var targetPos = MovingObject.Shift(checkPosition, direction);
                    if( MovingObject.CheckMovable(targetPos, Board)){
                        yield return targetPos;
                    }
                }                
            } 
        }

        public bool Action(string actionCommand){
            var totalActionCnt = actionCommand.Length;
            for (int actionCnt = 0; actionCnt < totalActionCnt; ++actionCnt){
                var command = actionCommand[actionCnt];
                switch (command)
                {
                    case 'U':
                        Move(Direction.Up);
                        break;
                    case 'D':
                        Move(Direction.Down);
                        break;
                    case 'L':
                        Move(Direction.Left);
                        break;
                    case 'R':
                        Move(Direction.Right);
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public abstract bool Move(Direction direction);
    }

    public class Cow : Pet{
        public override PetType PetType => PetType.Cow; 
        public override int TotalMoveCnt {get {return 1;}}
        public override bool Move(Direction direction){
            return MovingObject.StandardMove(this, direction);
        }
    }

    public class Pig : Pet
    {
        public override PetType PetType => PetType.Pig; 
        public override int TotalMoveCnt {get {return 2;}}
        public override bool Move(Direction direction){
            return MovingObject.StandardMove(this, direction);
        }
    }

    public class Rabbit : Pet
    {
        public override PetType PetType => PetType.Rabbit; 
        public override int TotalMoveCnt {get {return 3;}}
        public override bool Move(Direction direction){
            return MovingObject.StandardMove(this, direction);
        }
    }

    public class Dog : Pet
    {
        public override PetType PetType => PetType.Dog; 
        public override int TotalMoveCnt {get {return 2;}}
        public override bool Move(Direction direction){
            return MovingObject.StandardMove(this, direction);
        }
    }


    public class Cat : Pet
    {
        public override PetType PetType => PetType.Cat; 
        public override int TotalMoveCnt {get {return 2;}}
        public override bool Move(Direction direction){
            return MovingObject.StandardMove(this, direction);
        }
    }

    public class Board{
        public FloorType this[int x, int y]{
            get {return _board[x, y];}
            set { this._board[x, y] = value;}
        }

        public FloorType this[Pos pos]{
            get {return _board[pos.X, pos.Y];}
            set { this._board[pos.X, pos.Y] = value;}			
        }

        public FloorType this[int index]
        {
            get {
                int x = index % Width;
                int y = (index - x)/Width;
                return this[x, y];
            }
            set {
                
                int x = index % Width;
                int y = (index - x)/Width;
                this[x, y] = value;
                
            }
        }

        public int Width {get{return _size;}}
        public int Height {get{return _size;}}
        public (int, int) Size{get {return (_size, _size);}}
        static readonly int _size = 32;

        FloorType[,] _board = new FloorType[_size, _size];

        public Board(){
            int lastWidthIndex = Width-1;
            int lastHeightIndex = Height-1;
            for(int h = 0; h < Height; ++h){
                this[0, h] = FloorType.Wall;
                this[lastWidthIndex, h] = FloorType.Wall;
            }
            for ( int w = 1; w < Width ; ++w){
                this[w, 0] = FloorType.Wall;
                this[w, lastHeightIndex] = FloorType.Wall;
            }
        }

        public Board(Board originalBoard){
            int lastWidthIndex = Width-1;
            int lastHeightIndex = Height-1;
            for(int h = 0; h < Height; ++h){
                for ( int w = 0; w < Width ; ++w){
                    this[w, h] = originalBoard[w,h];
                }
            }
        }

        /// <summary>
        /// 初期配置からボードを生成。壁は作れないので注意。
        /// </summary>
        /// <param name="humans"></param>
        /// <param name="pets"></param>
        /// <returns></returns>
        public static Board Create(IMovingObject[] humans, IMovingObject[] pets)
        {
            var board = new Board();
            foreach(var human in humans)
            {
                board[human.Pos] = FloorType.Human;
            }
            foreach(var pet in pets)
            {
                board[pet.Pos] = FloorType.Pet;
            }
            return board;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBoardIndex(Pos pos){
            return Width * pos.Y + pos.X;
        }

        static Dictionary<FloorType, string> _floorTypeToCharDict = new Dictionary<FloorType, string>{
            {FloorType.None, "_"},
            {FloorType.Human, "H"},
            {FloorType.Pet, "P"},
            {FloorType.PetAndHuman, "B"},
            {FloorType.Wall, "#"},
        };

        public void Show()
        {
            for(int w = 0; w < Width; ++w){
                var lineBoardSB = new StringBuilder();
                for ( int h = 0; h < Height ; ++h){
                    lineBoardSB.Append(_floorTypeToCharDict[this[w, h]]);
                }
                Program.WriteLine(lineBoardSB.ToString());
            }
        }

        public static int CalcManhattanDistance(Pos pos1, Pos pos2)
        {
            int distX = Math.Abs(pos2.X-pos1.X);
            int distY = Math.Abs(pos2.Y-pos1.Y);
            return distX+distY;
        }

        public static bool IsInsideOfBoard(Pos pos)
        {
            bool xBoundary = (0 < pos.X) && (pos.X <31);
            bool yBoundary = (0 < pos.Y) && (pos.Y <31);
            return xBoundary && yBoundary;
         }
    }

}

namespace AtCoder
{

    /// <summary>
    /// 蟻本の実装そのまま
    /// </summary>
    public class UnionFindTree
    {
        public int Count { get; private set;}

        protected int[] _parent;

        protected int[] _rank;

        private UnionFindTree(){}
        public UnionFindTree(int treeSize)
        {
            Count = treeSize;
            _parent = new int[Count];
            _rank = new int[Count];
            for (int index = 0; index < Count; index++)
            {
                _parent[index] = index;
                _rank[index] = 0;
            }
        }

        public int FindRoot(int searchIndex)
        {
            if(_parent[searchIndex] == searchIndex)
            {
                return searchIndex;
            } else
            {
                return _parent[searchIndex] = FindRoot(_parent[searchIndex]);
            }
        }

        public virtual void Unite(int x, int y)
        {
            x = FindRoot(x);
            y = FindRoot(y);

            if(x == y){return;}

            if(_rank[x] < _rank[y])
            {
                _parent[x] = y;
            }
            else
            {
                _parent[y] = x;
                if(_rank[x] == _rank[y])
                {
                    _rank[x]++;
                }
            }
        }

        public bool IsSame(int x, int y)
        {
            return FindRoot(x) == FindRoot(y);
        }
    }

    /// <summary>
    /// ここから下はtakytankさんのテンプレートを拝借
    /// </summary>
    public static class ModCounting
    {
        private const long _p = ModInt.P;

        private static ModInt[] _factorial;
        private static ModInt[] _inverseFactorial;
        private static ModInt[] _inverse;
        private static ModInt[] _montmort;

        public static void InitializeFactorial(long max, bool withInverse = false)
        {
            if (withInverse) {
                _factorial = new ModInt[max + 1];
                _inverseFactorial = new ModInt[max + 1];
                _inverse = new ModInt[max + 1];

                _factorial[0] = _factorial[1] = 1;
                _inverseFactorial[0] = _inverseFactorial[1] = 1;
                _inverse[1] = 1;
                for (int i = 2; i <= max; i++) {
                    _factorial[i] = _factorial[i - 1] * i;
                    _inverse[i] = _p - _inverse[_p % i] * (_p / i);
                    _inverseFactorial[i] = _inverseFactorial[i - 1] * _inverse[i];
                }
            } else {
                _factorial = new ModInt[max + 1];
                _inverseFactorial = new ModInt[max + 1];

                _factorial[0] = _factorial[1] = 1;
                for (int i = 2; i <= max; i++) {
                    _factorial[i] = _factorial[i - 1] * i;
                }

                _inverseFactorial[max] = new ModInt(1) / _factorial[max];
                for (long i = max - 1; i >= 0; i--) {
                    _inverseFactorial[i] = _inverseFactorial[i + 1] * (i + 1);
                }
            }
        }

        public static void InitializeMontmort(long max)
        {
            _montmort = new ModInt[Math.Max(3, max + 1)];
            _montmort[0] = 1;
            _montmort[1] = 0;
            for (int i = 2; i < max + 1; i++) {
                _montmort[i] = (i - 1) * (_montmort[i - 1] + _montmort[i - 2]);
            }
        }

        public static ModInt Factorial(long n)
        {
            if (n < 0) {
                return 0;
            }

            return _factorial[n];
        }

        public static ModInt InverseFactorial(long n)
        {
            if (n < 0) {
                return 0;
            }

            return _inverseFactorial[n];
        }

        public static ModInt Inverse(long n)
        {
            if (n < 0) {
                return 0;
            }

            return _inverse[n];
        }

        public static ModInt Montmort(long n)
        {
            if (n < 0) {
                return 0;
            }

            return _montmort[n];
        }

        public static ModInt Permutation(long n, long k)
        {
            if (n < k || (n < 0 || k < 0)) {
                return 0;
            }

            return _factorial[n] * _inverseFactorial[n - k];
        }

        public static ModInt RepeatedPermutation(long n, long k)
        {
            long ret = 1;
            for (k %= _p - 1; k > 0; k >>= 1, n = n * n % _p) {
                if ((k & 1) == 1) {
                    ret = ret * n % _p;
                }
            }

            return ret;
        }

        public static ModInt Combination(long n, long k)
        {
            if (n < k || (n < 0 || k < 0)) {
                return 0;
            }

            return _factorial[n] * _inverseFactorial[k] * _inverseFactorial[n - k];
        }

        public static ModInt CombinationK(long n, long k)
        {
            ModInt ret = 1;
            for (int i = 0; i < k; i++) {
                ret *= (n - i) % _p;
                ret *= _inverse[i + 1];
            }

            return ret;
        }

        public static ModInt HomogeneousProduct(long n, long k)
        {
            if (n < 0 || k < 0) {
                return 0;
            }

            return Combination(n + k - 1, k);
        }

        public static ModInt HomogeneousProductK(long n, long k)
        {
            if (n < 0 || k < 0) {
                return 0;
            }

            return CombinationK(n + k - 1, k);
        }

        public static ModInt Catalan(long n)
        {
            if (n < 0) {
                return 0;
            }

            return Combination(2 * n, n) * Inverse(n + 1);
        }
    }

    [DebuggerTypeProxy(typeof(SegmentTree<>.DebugView))]
    public class SegmentTree<T> : IEnumerable<T>
    {
        private readonly int n_;
        private readonly T unit_;
        private readonly T[] tree_;
        private readonly Func<T, T, T> operate_;

        public int Count { get; }
        public T Top => tree_[1];

        public T this[int index]
        {
            get => tree_[index + n_];
            set => Update(index, value);
        }

        public SegmentTree(int count, T unit, Func<T, T, T> operate)
        {
            operate_ = operate;
            unit_ = unit;

            Count = count;
            n_ = 1;
            while (n_ < count) {
                n_ <<= 1;
            }

            tree_ = new T[n_ << 1];
            for (int i = 0; i < tree_.Length; i++) {
                tree_[i] = unit;
            }
        }

        public SegmentTree(T[] src, T unit, Func<T, T, T> operate)
            : this(src.Length, unit, operate)
        {
            for (int i = 0; i < src.Length; ++i) {
                tree_[i + n_] = src[i];
            }

            for (int i = n_ - 1; i > 0; --i) {
                tree_[i] = operate_(tree_[i << 1], tree_[(i << 1) | 1]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(int index, T value)
        {
            if (index >= Count) {
                return;
            }

            index += n_;
            tree_[index] = value;
            index >>= 1;
            while (index != 0) {
                tree_[index] = operate_(tree_[index << 1], tree_[(index << 1) | 1]);
                index >>= 1;
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public T Query(Range range) => Query(range.Start.Value, range.End.Value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Query(int left, int right)
        {
            if (left > right || right < 0 || left >= Count) {
                return unit_;
            }

            int l = left + n_;
            int r = right + n_;
            T valL = unit_;
            T valR = unit_;
            while (l < r) {
                if ((l & 1) != 0) {
                    valL = operate_(valL, tree_[l]);
                    ++l;
                }
                if ((r & 1) != 0) {
                    --r;
                    valR = operate_(tree_[r], valR);
                }

                l >>= 1;
                r >>= 1;
            }

            return operate_(valL, valR);
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int FindLeftest(Range range, Func<T, bool> check)
        //	=> FindLeftest(range.Start.Value, range.End.Value, check);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FindLeftest(int left, int right, Func<T, bool> check)
            => FindLeftestCore(left, right, 1, 0, n_, check);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindLeftestCore(int left, int right, int v, int l, int r, Func<T, bool> check)
        {
            if (check(tree_[v]) == false || r <= left || right <= l || Count <= left) {
                return right;
            } else if (v >= n_) {
                return v - n_;
            } else {
                int lc = v << 1;
                int rc = (v << 1) | 1;
                int mid = (l + r) >> 1;
                int vl = FindLeftestCore(left, right, lc, l, mid, check);
                if (vl != right) {
                    return vl;
                } else {
                    return FindLeftestCore(left, right, rc, mid, r, check);
                }
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public int FindRightest(Range range, Func<T, bool> check)
        //	=> FindRightest(range.Start.Value, range.End.Value, check);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int FindRightest(int left, int right, Func<T, bool> check)
            => FindRightestCore(left, right, 1, 0, n_, check);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindRightestCore(int left, int right, int v, int l, int r, Func<T, bool> check)
        {
            if (check(tree_[v]) == false || r <= left || right <= l || Count <= left) {
                return left - 1;
            } else if (v >= n_) {
                return v - n_;
            } else {
                int lc = v << 1;
                int rc = (v << 1) | 1;
                int mid = (l + r) >> 1;
                int vr = FindRightestCore(left, right, rc, mid, r, check);
                if (vr != left - 1) {
                    return vr;
                } else {
                    return FindRightestCore(left, right, lc, l, mid, check);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int MaxRight(int l, Predicate<T> satisfies)
        {
            if (l == Count) {
                return Count;
            }

            l += n_;
            var sum = unit_;
            do {
                while (l % 2 == 0) {
                    l >>= 1;
                }

                if (satisfies(operate_(sum, tree_[l])) == false) {
                    while (l < n_) {
                        l <<= 1;
                        var temp = operate_(sum, tree_[l]);
                        if (satisfies(temp)) {
                            sum = temp;
                            ++l;
                        }
                    }

                    return l - n_;
                }

                sum = operate_(sum, tree_[l]);
                ++l;
            } while ((l & -l) != l);

            return Count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int MinLeft(int r, Predicate<T> satisfies)
        {
            if (r == 0) {
                return 0;
            }

            r += n_;
            var sum = unit_;
            do {
                --r;
                while (r > 1 && (r % 2) != 0) {
                    r >>= 1;
                }

                if (satisfies(operate_(tree_[r], sum)) == false) {
                    while (r < n_) {
                        r = (r << 1) | 1;
                        var temp = operate_(tree_[r], sum);
                        if (satisfies(temp)) {
                            sum = temp;
                            --r;
                        }
                    }

                    return r + 1 - n_;
                }

                sum = operate_(tree_[r], sum);
            } while ((r & -r) != r);

            return 0;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++) {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [DebuggerDisplay("data= {" + nameof(data_) + "}", Name = "{" + nameof(key_) + ",nq}")]
        private struct DebugItem
        {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly string key_;
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private readonly T data_;
            public DebugItem(int l, int r, T data)
            {
                if (r - l == 1) {
                    key_ = $"[{l}]";
                } else {
                    key_ = $"[{l}-{r})";
                }

                data_ = data;
            }
        }

        private class DebugView
        {
            private readonly SegmentTree<T> tree_;
            public DebugView(SegmentTree<T> tree)
            {
                tree_ = tree;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public DebugItem[] Items
            {
                get
                {
                    var items = new List<DebugItem>(tree_.Count);
                    int length = tree_.n_;
                    while (length > 0) {
                        int unit = tree_.n_ / length;
                        for (int i = 0; i < length; i++) {
                            int l = i * unit;
                            int r = l + unit;
                            if (l < tree_.Count) {
                                int dataIndex = i + length;
                                items.Add(new DebugItem(
                                    l,
                                    r,
                                    tree_.tree_[dataIndex]));
                            }
                        }

                        length >>= 1;
                    }

                    return items.ToArray();
                }
            }
        }
    }

    public struct BitFlag
    {
        public static BitFlag Begin() => 0;
        public static BitFlag End(int bitCount) => 1 << bitCount;
        public static BitFlag FromBit(int bitNumber) => 1 << bitNumber;
        public static BitFlag Fill(int count) => (1 << count) - 1;

        public static IEnumerable<BitFlag> All(int n)
        {
            for (var f = Begin(); f < End(n); ++f) {
                yield return f;
            }
        }

        private readonly int flags_;
        public int Flag => flags_;
        public bool this[int bitNumber] => (flags_ & (1 << bitNumber)) != 0;
        public BitFlag(int flags) { flags_ = flags; }

        public bool Has(BitFlag target) => (flags_ & target.flags_) == target.flags_;
        public bool Has(int target) => (flags_ & target) == target;
        public bool HasBit(int bitNumber) => (flags_ & (1 << bitNumber)) != 0;
        public BitFlag OrBit(int bitNumber) => flags_ | (1 << bitNumber);
        public BitFlag AndBit(int bitNumber) => flags_ & (1 << bitNumber);
        public BitFlag XorBit(int bitNumber) => flags_ ^ (1 << bitNumber);
        public BitFlag ComplementOf(BitFlag sub) => flags_ ^ sub.flags_;
        public int PopCount() => BitOperations.PopCount((uint)flags_);

        public static BitFlag operator ++(BitFlag src) => new BitFlag(src.flags_ + 1);
        public static BitFlag operator --(BitFlag src) => new BitFlag(src.flags_ - 1);
        public static BitFlag operator |(BitFlag lhs, BitFlag rhs)
            => new BitFlag(lhs.flags_ | rhs.flags_);
        public static BitFlag operator |(BitFlag lhs, int rhs)
            => new BitFlag(lhs.flags_ | rhs);
        public static BitFlag operator |(int lhs, BitFlag rhs)
            => new BitFlag(lhs | rhs.flags_);
        public static BitFlag operator &(BitFlag lhs, BitFlag rhs)
            => new BitFlag(lhs.flags_ & rhs.flags_);
        public static BitFlag operator &(BitFlag lhs, int rhs)
            => new BitFlag(lhs.flags_ & rhs);
        public static BitFlag operator &(int lhs, BitFlag rhs)
            => new BitFlag(lhs & rhs.flags_);
        public static BitFlag operator ^(BitFlag lhs, BitFlag rhs)
            => new BitFlag(lhs.flags_ ^ rhs.flags_);
        public static BitFlag operator ^(BitFlag lhs, int rhs)
            => new BitFlag(lhs.flags_ ^ rhs);
        public static BitFlag operator ^(int lhs, BitFlag rhs)
            => new BitFlag(lhs ^ rhs.flags_);
        public static BitFlag operator <<(BitFlag bit, int shift) => bit.flags_ << shift;
        public static BitFlag operator >>(BitFlag bit, int shift) => bit.flags_ >> shift;

        public static bool operator <(BitFlag lhs, BitFlag rhs) => lhs.flags_ < rhs.flags_;
        public static bool operator <(BitFlag lhs, int rhs) => lhs.flags_ < rhs;
        public static bool operator <(int lhs, BitFlag rhs) => lhs < rhs.flags_;
        public static bool operator >(BitFlag lhs, BitFlag rhs) => lhs.flags_ > rhs.flags_;
        public static bool operator >(BitFlag lhs, int rhs) => lhs.flags_ > rhs;
        public static bool operator >(int lhs, BitFlag rhs) => lhs > rhs.flags_;
        public static bool operator <=(BitFlag lhs, BitFlag rhs) => lhs.flags_ <= rhs.flags_;
        public static bool operator <=(BitFlag lhs, int rhs) => lhs.flags_ <= rhs;
        public static bool operator <=(int lhs, BitFlag rhs) => lhs <= rhs.flags_;
        public static bool operator >=(BitFlag lhs, BitFlag rhs) => lhs.flags_ >= rhs.flags_;
        public static bool operator >=(BitFlag lhs, int rhs) => lhs.flags_ >= rhs;
        public static bool operator >=(int lhs, BitFlag rhs) => lhs >= rhs.flags_;

        public static implicit operator BitFlag(int t) => new BitFlag(t);
        public static implicit operator int(BitFlag t) => t.flags_;

        public override string ToString() => $"{Convert.ToString(flags_, 2).PadLeft(32, '0')} ({flags_})";

        public SubBitsEnumerator SubBits => new SubBitsEnumerator(flags_);
        public struct SubBitsEnumerator : IEnumerable<BitFlag>
        {
            private readonly int flags_;
            public SubBitsEnumerator(int flags)
            {
                flags_ = flags;
            }

            IEnumerator<BitFlag> IEnumerable<BitFlag>.GetEnumerator() => new Enumerator(flags_);
            IEnumerator IEnumerable.GetEnumerator() => new Enumerator(flags_);
            public Enumerator GetEnumerator() => new Enumerator(flags_);
            public struct Enumerator : IEnumerator<BitFlag>
            {
                private readonly int src_;
                public BitFlag Current { get; private set; }
                object IEnumerator.Current => Current;

                public Enumerator(int flags)
                {
                    src_ = flags;
                    Current = flags + 1;
                }

                public void Dispose() { }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool MoveNext() => (Current = --Current & src_) > 0;
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public void Reset() => Current = src_;
            }
        }
    }

    public class HashMap<TKey, TValue> : Dictionary<TKey, TValue>
    {
        public static HashMap<TKey, TValue> Merge(
            HashMap<TKey, TValue> src1,
            HashMap<TKey, TValue> src2,
            Func<TValue, TValue, TValue> mergeValues)
        {
            if (src1.Count < src2.Count) {
                (src1, src2) = (src2, src1);
            }

            foreach (var key in src2.Keys) {
                src1[key] = mergeValues(src1[key], src2[key]);
            }

            return src1;
        }

        private readonly Func<TKey, TValue> initialzier_;
        public HashMap(Func<TKey, TValue> initialzier)
            : base()
        {
            initialzier_ = initialzier;
        }

        public HashMap(Func<TKey, TValue> initialzier, int capacity)
            : base(capacity)
        {
            initialzier_ = initialzier;
        }

        new public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out TValue value)) {
                    return value;
                } else {
                    var init = initialzier_(key);
                    base[key] = init;
                    return init;
                }
            }

            set { base[key] = value; }
        }

        public HashMap<TKey, TValue> Merge(
            HashMap<TKey, TValue> src,
            Func<TValue, TValue, TValue> mergeValues)
        {
            foreach (var key in src.Keys) {
                this[key] = mergeValues(this[key], src[key]);
            }

            return this;
        }
    }

    public class JagList2<T> where T : struct
    {
        private readonly int n_;
        private readonly List<T>[] tempValues_;
        private T[][] values_;

        public int Count => n_;
        public List<T>[] Raw => tempValues_;
        public T[][] Values => values_;
        public T[] this[int index] => values_[index];

        public JagList2(int n)
        {
            n_ = n;
            tempValues_ = new List<T>[n];
            for (int i = 0; i < n; ++i) {
                tempValues_[i] = new List<T>();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int i, T value) => tempValues_[i].Add(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Build()
        {
            values_ = new T[n_][];
            for (int i = 0; i < values_.Length; ++i) {
                values_[i] = tempValues_[i].ToArray();
            }
        }
    }

    public class DijkstraQ
    {
        private int count_ = 0;
        private long[] distanceHeap_;
        private int[] vertexHeap_;

        public int Count => count_;
        public DijkstraQ()
        {
            distanceHeap_ = new long[8];
            vertexHeap_ = new int[8];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(long distance, int v)
        {
            if (distanceHeap_.Length == count_) {
                var newDistanceHeap = new long[distanceHeap_.Length << 1];
                var newVertexHeap = new int[vertexHeap_.Length << 1];
                Unsafe.CopyBlock(
                    ref Unsafe.As<long, byte>(ref newDistanceHeap[0]),
                    ref Unsafe.As<long, byte>(ref distanceHeap_[0]),
                    (uint)(8 * count_));
                Unsafe.CopyBlock(
                    ref Unsafe.As<int, byte>(ref newVertexHeap[0]),
                    ref Unsafe.As<int, byte>(ref vertexHeap_[0]),
                    (uint)(4 * count_));
                distanceHeap_ = newDistanceHeap;
                vertexHeap_ = newVertexHeap;
            }

            ref var dRef = ref distanceHeap_[0];
            ref var vRef = ref vertexHeap_[0];
            Unsafe.Add(ref dRef, count_) = distance;
            Unsafe.Add(ref vRef, count_) = v;
            ++count_;

            int c = count_ - 1;
            while (c > 0) {
                int p = (c - 1) >> 1;
                var tempD = Unsafe.Add(ref dRef, p);
                if (tempD <= distance) {
                    break;
                } else {
                    Unsafe.Add(ref dRef, c) = tempD;
                    Unsafe.Add(ref vRef, c) = Unsafe.Add(ref vRef, p);
                    c = p;
                }
            }

            Unsafe.Add(ref dRef, c) = distance;
            Unsafe.Add(ref vRef, c) = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (long distance, int v) Dequeue()
        {
            ref var dRef = ref distanceHeap_[0];
            ref var vRef = ref vertexHeap_[0];
            (long distance, int v) ret = (dRef, vRef);
            int n = count_ - 1;

            var distance = Unsafe.Add(ref dRef, n);
            var vertex = Unsafe.Add(ref vRef, n);
            int p = 0;
            int c = (p << 1) + 1;
            while (c < n) {
                if (c != n - 1 && Unsafe.Add(ref dRef, c + 1) < Unsafe.Add(ref dRef, c)) {
                    ++c;
                }

                var tempD = Unsafe.Add(ref dRef, c);
                if (distance > tempD) {
                    Unsafe.Add(ref dRef, p) = tempD;
                    Unsafe.Add(ref vRef, p) = Unsafe.Add(ref vRef, c);
                    p = c;
                    c = (p << 1) + 1;
                } else {
                    break;
                }
            }

            Unsafe.Add(ref dRef, p) = distance;
            Unsafe.Add(ref vRef, p) = vertex;
            --count_;

            return ret;
        }
    }

    public struct ModInt
    {
        //public const long P = 1000000007;
        public const long P = 998244353;
        //public const long P = 2;
        public const long ROOT = 3;

        // (924844033, 5)
        // (998244353, 3)
        // (1012924417, 5)
        // (167772161, 3)
        // (469762049, 3)
        // (1224736769, 3)

        private long value_;

        public static ModInt New(long value, bool mods) => new ModInt(value, mods);
        public ModInt(long value) => value_ = value;
        public ModInt(long value, bool mods)
        {
            if (mods) {
                value %= P;
                if (value < 0) {
                    value += P;
                }
            }

            value_ = value;
        }

        public static ModInt operator +(ModInt lhs, ModInt rhs)
        {
            lhs.value_ = (lhs.value_ + rhs.value_) % P;
            return lhs;
        }
        public static ModInt operator +(long lhs, ModInt rhs)
        {
            rhs.value_ = (lhs + rhs.value_) % P;
            return rhs;
        }
        public static ModInt operator +(ModInt lhs, long rhs)
        {
            lhs.value_ = (lhs.value_ + rhs) % P;
            return lhs;
        }

        public static ModInt operator -(ModInt lhs, ModInt rhs)
        {
            lhs.value_ = (P + lhs.value_ - rhs.value_) % P;
            return lhs;
        }
        public static ModInt operator -(long lhs, ModInt rhs)
        {
            rhs.value_ = (P + lhs - rhs.value_) % P;
            return rhs;
        }
        public static ModInt operator -(ModInt lhs, long rhs)
        {
            lhs.value_ = (P + lhs.value_ - rhs) % P;
            return lhs;
        }

        public static ModInt operator *(ModInt lhs, ModInt rhs)
        {
            lhs.value_ = lhs.value_ * rhs.value_ % P;
            return lhs;
        }
        public static ModInt operator *(long lhs, ModInt rhs)
        {
            rhs.value_ = lhs * rhs.value_ % P;
            return rhs;
        }
        public static ModInt operator *(ModInt lhs, long rhs)
        {
            lhs.value_ = lhs.value_ * rhs % P;
            return lhs;
        }

        public static ModInt operator /(ModInt lhs, ModInt rhs)
            => lhs * Inverse(rhs);

        public static implicit operator ModInt(long n) => new ModInt(n, true);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModInt Inverse(ModInt value) => Pow(value, P - 2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModInt Pow(ModInt value, long k) => Pow(value.value_, k);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModInt Pow(long value, long k)
        {
            long ret = 1;
            while (k > 0) {
                if ((k & 1) != 0) {
                    ret = ret * value % P;
                }

                value = value * value % P;
                k >>= 1;
            }

            return new ModInt(ret);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ToLong() => value_;
        public override string ToString() => value_.ToString();
    }

    public static class Helper
    {
        public static long INF => 1L << 50;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Clamp<T>(this T value, T min, T max) where T : struct, IComparable<T>
        {
            if (value.CompareTo(min) <= 0) {
                return min;
            }

            if (value.CompareTo(max) >= 0) {
                return max;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMin<T>(this ref T target, T value) where T : struct, IComparable<T>
            => target = target.CompareTo(value) > 0 ? value : target;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMin<T>(this ref T target, T value, Action<T> onUpdated)
            where T : struct, IComparable<T>
        {
            if (target.CompareTo(value) > 0) {
                target = value;
                onUpdated(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMax<T>(this ref T target, T value) where T : struct, IComparable<T>
            => target = target.CompareTo(value) < 0 ? value : target;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMax<T>(this ref T target, T value, Action<T> onUpdated)
            where T : struct, IComparable<T>
        {
            if (target.CompareTo(value) < 0) {
                target = value;
                onUpdated(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BinarySearchOKNG(long ok, long ng, Func<long, bool> satisfies)
        {
            while (ng - ok > 1) {
                long mid = (ok + ng) / 2;
                if (satisfies(mid)) {
                    ok = mid;
                } else {
                    ng = mid;
                }
            }

            return ok;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BinarySearchNGOK(long ng, long ok, Func<long, bool> satisfies)
        {
            while (ok - ng > 1) {
                long mid = (ok + ng) / 2;
                if (satisfies(mid)) {
                    ok = mid;
                } else {
                    ng = mid;
                }
            }

            return ok;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Array1<T>(int n, T initialValue) where T : struct
            => new T[n].Fill(initialValue);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Array1<T>(int n, Func<int, T> initializer)
            => Enumerable.Range(0, n).Select(x => initializer(x)).ToArray();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Fill<T>(this T[] array, T value)
            where T : struct
        {
            array.AsSpan().Fill(value);
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[,] Array2<T>(int n, int m, T initialValule) where T : struct
            => new T[n, m].Fill(initialValule);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[,] Array2<T>(int n, int m, Func<int, int, T> initializer)
        {
            var array = new T[n, m];
            for (int i = 0; i < n; ++i) {
                for (int j = 0; j < m; ++j) {
                    array[i, j] = initializer(i, j);
                }
            }

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[,] Fill<T>(this T[,] array, T initialValue)
            where T : struct
        {
            MemoryMarshal.CreateSpan<T>(ref array[0, 0], array.Length).Fill(initialValue);
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this T[,] array, int i)
            => MemoryMarshal.CreateSpan<T>(ref array[i, 0], array.GetLength(1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[,,] Array3<T>(int n1, int n2, int n3, T initialValue)
            where T : struct
            => new T[n1, n2, n3].Fill(initialValue);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[,,] Fill<T>(this T[,,] array, T initialValue)
            where T : struct
        {
            MemoryMarshal.CreateSpan<T>(ref array[0, 0, 0], array.Length).Fill(initialValue);
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this T[,,] array, int i, int j)
            => MemoryMarshal.CreateSpan<T>(ref array[i, j, 0], array.GetLength(2));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[,,,] Array4<T>(int n1, int n2, int n3, int n4, T initialValue)
            where T : struct
            => new T[n1, n2, n3, n4].Fill(initialValue);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[,,,] Fill<T>(this T[,,,] array, T initialValue)
            where T : struct
        {
            MemoryMarshal.CreateSpan<T>(ref array[0, 0, 0, 0], array.Length).Fill(initialValue);
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this T[,,,] array, int i, int j, int k)
            => MemoryMarshal.CreateSpan<T>(ref array[i, j, k, 0], array.GetLength(3));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Merge<T>(ReadOnlySpan<T> first, ReadOnlySpan<T> second) where T : IComparable<T>
        {
            var ret = new T[first.Length + second.Length];
            int p = 0;
            int q = 0;
            while (p < first.Length || q < second.Length) {
                if (p == first.Length) {
                    ret[p + q] = second[q];
                    q++;
                    continue;
                }

                if (q == second.Length) {
                    ret[p + q] = first[p];
                    p++;
                    continue;
                }

                if (first[p].CompareTo(second[q]) < 0) {
                    ret[p + q] = first[p];
                    p++;
                } else {
                    ret[p + q] = second[q];
                    q++;
                }
            }

            return ret;
        }

        private static readonly int[] delta4_ = { 1, 0, -1, 0, 1 };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<(int i, int j)> Adjacence4(int i, int j, int imax, int jmax)
        {
            for (int dn = 0; dn < 4; ++dn) {
                int d4i = i + delta4_[dn];
                int d4j = j + delta4_[dn + 1];
                if ((uint)d4i < (uint)imax && (uint)d4j < (uint)jmax) {
                    yield return (d4i, d4j);
                }
            }
        }

        private static readonly int[] delta8_ = { 1, 0, -1, 0, 1, 1, -1, -1, 1 };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<(int i, int j)> Adjacence8(int i, int j, int imax, int jmax)
        {
            for (int dn = 0; dn < 8; ++dn) {
                int d8i = i + delta8_[dn];
                int d8j = j + delta8_[dn + 1];
                if ((uint)d8i < (uint)imax && (uint)d8j < (uint)jmax) {
                    yield return (d8i, d8j);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IEnumerable<int> SubBitsOf(int bit)
        {
            for (int sub = bit; sub > 0; sub = --sub & bit) {
                yield return sub;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Reverse(string src)
        {
            var chars = src.ToCharArray();
            for (int i = 0, j = chars.Length - 1; i < j; ++i, --j) {
                var tmp = chars[i];
                chars[i] = chars[j];
                chars[j] = tmp;
            }

            return new string(chars);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Exchange(string src, char a, char b)
        {
            var chars = src.ToCharArray();
            for (int i = 0; i < chars.Length; i++) {
                if (chars[i] == a) {
                    chars[i] = b;
                } else if (chars[i] == b) {
                    chars[i] = a;
                }
            }

            return new string(chars);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap(this string str, int i, int j)
        {
            var span = str.AsWriteableSpan();
            (span[i], span[j]) = (span[j], span[i]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char Replace(this string str, int index, char c)
        {
            var span = str.AsWriteableSpan();
            char old = span[index];
            span[index] = c;
            return old;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<char> AsWriteableSpan(this string str)
        {
            var span = str.AsSpan();
            return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Join<T>(this IEnumerable<T> values, string separator = "")
            => string.Join(separator, values);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string JoinNL<T>(this IEnumerable<T> values)
            => string.Join(Environment.NewLine, values);
    }

    public static class Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(this List<T> list)
        {
            return Unsafe.As<FakeList<T>>(list).Array.AsSpan(0, list.Count);
        }

        private class FakeList<T>
        {
            public T[] Array = null;
        }
    }

    public class Scanner : IDisposable
    {
        private const int BUFFER_SIZE = 1024;
        private const int ASCII_SPACE = 32;
        private const int ASCII_CHAR_BEGIN = 33;
        private const int ASCII_CHAR_END = 126;
        private readonly string filePath_;
        private readonly Stream stream_;
        private readonly byte[] buf_ = new byte[BUFFER_SIZE];
        private int length_ = 0;
        private int index_ = 0;
        private bool isEof_ = false;

        public Scanner(string file = "")
        {
            if (string.IsNullOrWhiteSpace(file)) {
                stream_ = Console.OpenStandardInput();
            } else {
                filePath_ = file;
                stream_ = new FileStream(file, FileMode.Open);
            }

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) {
                AutoFlush = false
            });
        }

        public void Dispose()
        {
            Console.Out.Flush();
            stream_.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string NextLine()
        {
            var sb = new StringBuilder();
            for (var b = Char(); b >= ASCII_SPACE && b <= ASCII_CHAR_END; b = (char)Read()) {
                sb.Append(b);
            }

            return sb.ToString();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public char Char()
        {
            byte b;
            do {
                b = Read();
            } while (b < ASCII_CHAR_BEGIN || ASCII_CHAR_END < b);

            return (char)b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string String()
        {
            var sb = new StringBuilder();
            for (var b = Char(); b >= ASCII_CHAR_BEGIN && b <= ASCII_CHAR_END; b = (char)Read()) {
                sb.Append(b);
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string[] ArrayString(int length)
        {
            var array = new string[length];
            for (int i = 0; i < length; ++i) {
                array[i] = String();
            }

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Int() => (int)Long();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Int(int offset) => Int() + offset;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int, int) Int2(int offset = 0)
            => (Int(offset), Int(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int, int, int) Int3(int offset = 0)
            => (Int(offset), Int(offset), Int(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int, int, int, int) Int4(int offset = 0)
            => (Int(offset), Int(offset), Int(offset), Int(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int, int, int, int, int) Int5(int offset = 0)
            => (Int(offset), Int(offset), Int(offset), Int(offset), Int(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int[] ArrayInt(int length, int offset = 0)
        {
            var array = new int[length];
            for (int i = 0; i < length; ++i) {
                array[i] = Int(offset);
            }

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Long()
        {
            long ret = 0;
            byte b;
            bool ng = false;
            do {
                b = Read();
            } while (b != '-' && (b < '0' || '9' < b));

            if (b == '-') {
                ng = true;
                b = Read();
            }

            for (; true; b = Read()) {
                if (b < '0' || '9' < b) {
                    return ng ? -ret : ret;
                } else {
                    ret = ret * 10 + b - '0';
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long Long(long offset) => Long() + offset;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (long, long) Long2(long offset = 0)
            => (Long(offset), Long(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (long, long, long) Long3(long offset = 0)
            => (Long(offset), Long(offset), Long(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (long, long, long, long) Long4(long offset = 0)
            => (Long(offset), Long(offset), Long(offset), Long(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (long, long, long, long, long) Long5(long offset = 0)
            => (Long(offset), Long(offset), Long(offset), Long(offset), Long(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long[] ArrayLong(int length, long offset = 0)
        {
            var array = new long[length];
            for (int i = 0; i < length; ++i) {
                array[i] = Long(offset);
            }

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigInteger Big() => new BigInteger(Long());
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigInteger Big(long offset) => Big() + offset;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (BigInteger, BigInteger) Big2(long offset = 0)
            => (Big(offset), Big(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (BigInteger, BigInteger, BigInteger) Big3(long offset = 0)
            => (Big(offset), Big(offset), Big(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (BigInteger, BigInteger, BigInteger, BigInteger) Big4(long offset = 0)
            => (Big(offset), Big(offset), Big(offset), Big(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (BigInteger, BigInteger, BigInteger, BigInteger, BigInteger) Big5(long offset = 0)
            => (Big(offset), Big(offset), Big(offset), Big(offset), Big(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BigInteger[] ArrayBig(int length, long offset = 0)
        {
            var array = new BigInteger[length];
            for (int i = 0; i < length; ++i) {
                array[i] = Big(offset);
            }

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Double() => double.Parse(String(), CultureInfo.InvariantCulture);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Double(double offset) => Double() + offset;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (double, double) Double2(double offset = 0)
            => (Double(offset), Double(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (double, double, double) Double3(double offset = 0)
            => (Double(offset), Double(offset), Double(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (double, double, double, double) Double4(double offset = 0)
            => (Double(offset), Double(offset), Double(offset), Double(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (double, double, double, double, double) Double5(double offset = 0)
            => (Double(offset), Double(offset), Double(offset), Double(offset), Double(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double[] ArrayDouble(int length, double offset = 0)
        {
            var array = new double[length];
            for (int i = 0; i < length; ++i) {
                array[i] = Double(offset);
            }

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal Decimal() => decimal.Parse(String(), CultureInfo.InvariantCulture);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal Decimal(decimal offset) => Decimal() + offset;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (decimal, decimal) Decimal2(decimal offset = 0)
            => (Decimal(offset), Decimal(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (decimal, decimal, decimal) Decimal3(decimal offset = 0)
            => (Decimal(offset), Decimal(offset), Decimal(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (decimal, decimal, decimal, decimal) Decimal4(decimal offset = 0)
            => (Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (decimal, decimal, decimal, decimal, decimal) Decimal5(decimal offset = 0)
            => (Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset), Decimal(offset));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal[] ArrayDecimal(int length, decimal offset = 0)
        {
            var array = new decimal[length];
            for (int i = 0; i < length; ++i) {
                array[i] = Decimal(offset);
            }

            return array;
        }

        private byte Read()
        {
            if (isEof_) {
                throw new EndOfStreamException();
            }

            if (index_ >= length_) {
                index_ = 0;
                if ((length_ = stream_.Read(buf_, 0, BUFFER_SIZE)) <= 0) {
                    isEof_ = true;
                    return 0;
                }
            }

            return buf_[index_++];
        }

        public void Save(string text)
        {
            if (string.IsNullOrWhiteSpace(filePath_)) {
                return;
            }

            File.WriteAllText(filePath_ + "_output.txt", text);
        }
    }
}


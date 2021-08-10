using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

public enum BattleState
{
    StartBattle,
    ActionSelection,
    MovementSelection,
    PerformMovement,
    Busy,
    PartySelectScreen,
    ItemSelectScreen,
    ForgetMovement,
    LoseTurn,
    FinishBattle
}

public enum BattleType
{
    WildPokemon,
    Trainer,
    Leader
}

public class BattleManager : MonoBehaviour
{
    [SerializeField] BattleUnit playerUnit;

    [SerializeField] BattleUnit enemyUnit;

    [SerializeField] BattleDialogBox battleDialogBox;

    [SerializeField] PartyHUD partyHUD;

    [SerializeField] SelectionMovementUI selectMoveUI;

    [SerializeField] GameObject pokeball;

    public BattleState state;

    public BattleType type;

    public event Action<bool> OnBattleFinish;

    private PokemonParty playerParty;
    private Pokemon wildPokemon;


    private float timeSinceLastClick;
    [SerializeField] float timeBetweenClicks = 1.0f;


    private int currentSelectedAction;
    private int currentSelectedMovement;
    private int currentSelectedPokemon;

    private int escapeAttempts;
    private MoveBase moveToLearn;

    public AudioClip attackClip, damageClip, levelUpClip, pokeballClip, faintedClip, endBattleClip;

    public void HandleStartBattle(PokemonParty playerParty, Pokemon wildPokemon)
    {
        type = BattleType.WildPokemon;
        escapeAttempts = 0;
        this.playerParty = playerParty;
        this.wildPokemon = wildPokemon;
        StartCoroutine(SetupBattle());
    }

    public void HandleStartTrainerBattle(PokemonParty playerParty, PokemonParty trainerParty, bool isLeader)
    {
        type = (isLeader ? BattleType.Leader : BattleType.Trainer);
        //TODO: el resto dde batalla contra NPC
    }

    public IEnumerator SetupBattle()
    {
        state = BattleState.StartBattle;

        playerUnit.SetupPokemon(playerParty.GetFirstNonFaintedPokemon());

        battleDialogBox.SetPokemonMovements(playerUnit.Pokemon.Moves);

        enemyUnit.SetupPokemon(wildPokemon);

        partyHUD.InitPartyHUD();

        yield return battleDialogBox.SetDialog($"¡Un {enemyUnit.Pokemon.Base.Name} salvaje apareció!");

        yield return ChooseFirstTurn(true);
    }

    IEnumerator ChooseFirstTurn(bool showFirstMsg = false)
    {
        if (enemyUnit.Pokemon.Speed > playerUnit.Pokemon.Speed)
        {
            battleDialogBox.ToggleDialogText(true);
            battleDialogBox.ToggleActions(false);
            battleDialogBox.ToggleMovements(false);
            if (showFirstMsg)
            {
                yield return battleDialogBox.SetDialog("El enemigo ataca primero.");
            }
            
            yield return PerformEnemyMovement();
        }
        else
        {
            PlayerActionSelection();
        }
    }

    void BattleFinish(bool playerHasWon)
    {
        SoundManager.SharedInstance.PlaySound(endBattleClip);
        state = BattleState.FinishBattle;
        playerParty.Pokemons.ForEach(p => p.OnBattleFinish());
        OnBattleFinish(playerHasWon);
    }

    void PlayerActionSelection()
    {
        state = BattleState.ActionSelection;
        StartCoroutine(battleDialogBox.SetDialog("Selecciona una acción"));
        battleDialogBox.ToggleDialogText(true);
        battleDialogBox.ToggleActions(true);
        battleDialogBox.ToggleMovements(false);
        currentSelectedAction = 0;
        battleDialogBox.SelectAction(currentSelectedAction);
    }

    void PlayerMovementSelection()
    {
        state = BattleState.MovementSelection;
        battleDialogBox.ToggleDialogText(false);
        battleDialogBox.ToggleActions(false);
        battleDialogBox.ToggleMovements(true);
        currentSelectedMovement = 0;
        battleDialogBox.SelectMovement(currentSelectedMovement, playerUnit.Pokemon.Moves[currentSelectedMovement]);
    }

    void OpenPartySelectionScreen()
    {
        state = BattleState.PartySelectScreen;
        partyHUD.SetPartyData(playerParty.Pokemons);
        partyHUD.gameObject.SetActive(true);
        currentSelectedPokemon = playerParty.GetPositionFromPokemon(playerUnit.Pokemon);
        partyHUD.UpdateSelectedPokemon(currentSelectedPokemon);

    }

    void OpenInventoryScreen()
    {
        //TODO: Implementar Inventario y lógica de ítems
        print("Abrir inventario");
        battleDialogBox.ToggleActions(false);
        StartCoroutine(ThrowPokeball());
    }


    public void HandleUpdate()
    {
        timeSinceLastClick += Time.deltaTime;

        if (timeSinceLastClick < timeBetweenClicks || battleDialogBox.isWriting)
        {
            return;
        }

        if (state == BattleState.ActionSelection)
        {
            HandlePlayerActionSelection();
        }
        else if (state == BattleState.MovementSelection)
        {
            HandlePlayerMovementSelection();
        }
        else if (state == BattleState.PartySelectScreen)
        {
            HandlePlayerPartySelection();
        }
        else if (state == BattleState.LoseTurn)
        {
            StartCoroutine(PerformEnemyMovement());
        }
        else if (state == BattleState.ForgetMovement)
        {
            selectMoveUI.HandleForgetMoveSelection((moveIndex) =>
                {
                    if (moveIndex < 0)
                    {
                        timeSinceLastClick = 0;
                        return;
                    }

                    StartCoroutine(ForgetOldMove(moveIndex));
                });
        }
    }


    IEnumerator ForgetOldMove(int moveIndex)
    {
        selectMoveUI.gameObject.SetActive(false);
        if (moveIndex == PokemonBase.NUMBER_OF_LEARNABLE_MOVES)
        {
            yield return
                battleDialogBox.SetDialog($"{playerUnit.Pokemon.Base.Name} no ha aprendido {moveToLearn.Name}");
        }
        else
        {
            var selectedMove = playerUnit.Pokemon.Moves[moveIndex].Base;
            yield return
               battleDialogBox.SetDialog($"{playerUnit.Pokemon.Base.Name} olvidó {selectedMove.Name} y aprendió {moveToLearn.Name}");
            playerUnit.Pokemon.Moves[moveIndex] = new Move(moveToLearn);

        }

        moveToLearn = null;
        //TODO: revisar más adelante cuando haya entrenadores 
        state = BattleState.FinishBattle;

    }

    void HandlePlayerActionSelection()
    {

        if (Input.GetAxisRaw("Vertical") != 0)
        {
            timeSinceLastClick = 0;

            currentSelectedAction = (currentSelectedAction + 2) % 4;

            battleDialogBox.SelectAction(currentSelectedAction);
        }
        else if (Input.GetAxisRaw("Horizontal") != 0)
        {
            timeSinceLastClick = 0;
            currentSelectedAction = (currentSelectedAction + 1) % 2 +
                                    2 * Mathf.FloorToInt(currentSelectedAction / 2);
            battleDialogBox.SelectAction(currentSelectedAction);
        }

        if (Input.GetAxisRaw("Submit") != 0)
        {
            timeSinceLastClick = 0;
            if (currentSelectedAction == 0)
            {
                //Luchar
                PlayerMovementSelection();
            }
            else if (currentSelectedAction == 1)
            {
                //Cambiar Pokemon
                OpenPartySelectionScreen();
            }
            else if (currentSelectedAction == 2)
            {
                //Mochila
                OpenInventoryScreen();
            }
            else if (currentSelectedAction == 3)
            {
                //Huir
                StartCoroutine(TryToEscapeFromBattle());
            }
        }
    }


    void HandlePlayerMovementSelection()
    {

        /// 0  1
        /// 2  3
        if (Input.GetAxisRaw("Vertical") != 0)
        {
            timeSinceLastClick = 0;
            var oldSelectedMovement = currentSelectedMovement;
            currentSelectedMovement = (currentSelectedMovement + 2) % 4;
            if (currentSelectedMovement >= playerUnit.Pokemon.Moves.Count)
            {
                currentSelectedMovement = oldSelectedMovement;
            }

            battleDialogBox.SelectMovement(currentSelectedMovement, playerUnit.Pokemon.Moves[currentSelectedMovement]);

        }
        else if (Input.GetAxisRaw("Horizontal") != 0)
        {

            timeSinceLastClick = 0;
            var oldSelectedMovement = currentSelectedMovement;
            currentSelectedMovement = (currentSelectedMovement + 1) % 2 +
                                      2 * Mathf.FloorToInt(currentSelectedMovement / 2);

            if (currentSelectedMovement >= playerUnit.Pokemon.Moves.Count)
            {
                currentSelectedMovement = oldSelectedMovement;
            }
            battleDialogBox.SelectMovement(currentSelectedMovement, playerUnit.Pokemon.Moves[currentSelectedMovement]);

        }
        if (Input.GetAxisRaw("Submit") != 0)
        {
            timeSinceLastClick = 0;
            battleDialogBox.ToggleMovements(false);
            battleDialogBox.ToggleDialogText(true);
            StartCoroutine(PerformPlayerMovement());
        }

        if (Input.GetAxisRaw("Cancel") != 0)
        {
            PlayerActionSelection();
        }
    }

    void HandlePlayerPartySelection()
    {

        /// 0  1
        /// 2  3
        /// 4  5
        if (Input.GetAxisRaw("Vertical") != 0)
        {
            timeSinceLastClick = 0;
            currentSelectedPokemon -= (int)Input.GetAxisRaw("Vertical") * 2;

        }
        else if (Input.GetAxisRaw("Horizontal") != 0)
        {
            timeSinceLastClick = 0;
            currentSelectedPokemon += (int)Input.GetAxisRaw("Horizontal");
        }

        currentSelectedPokemon = Mathf.Clamp(currentSelectedPokemon,
           0, playerParty.Pokemons.Count - 1);
        partyHUD.UpdateSelectedPokemon(currentSelectedPokemon);

        if (Input.GetAxisRaw("Submit") != 0)
        {
            timeSinceLastClick = 0;
            var selectedPokemon = playerParty.Pokemons[currentSelectedPokemon];
            if (selectedPokemon.HP <= 0)
            {
                partyHUD.SetMessage("No puedes enviar un pokemon debilitado");
                return;
            }
            else if (selectedPokemon == playerUnit.Pokemon)
            {
                partyHUD.SetMessage("No puedes cambiar el pokemon en batalla");
                return;
            }

            partyHUD.gameObject.SetActive(false);
            state = BattleState.Busy;
            StartCoroutine(SwitchPokemon(selectedPokemon));
        }

        if (Input.GetAxisRaw("Cancel") != 0)
        {
            partyHUD.gameObject.SetActive(false);
            PlayerActionSelection();
        }

    }

    IEnumerator PerformPlayerMovement()
    {
        state = BattleState.PerformMovement;

        Move move = playerUnit.Pokemon.Moves[currentSelectedMovement];
        if (move.Pp <= 0)
        {
            PlayerMovementSelection();
            yield break;
        }
        yield return RunMovement(playerUnit, enemyUnit, move);

        if (state == BattleState.PerformMovement)
        {
            StartCoroutine(PerformEnemyMovement());
        }

    }

    IEnumerator PerformEnemyMovement()
    {
        state = BattleState.PerformMovement;

        Move move = enemyUnit.Pokemon.RandomMove();

        yield return RunMovement(enemyUnit, playerUnit, move);

        if (state == BattleState.PerformMovement)
        {
            PlayerActionSelection();
        }


    }


    IEnumerator RunMovement(BattleUnit attacker, BattleUnit target, Move move)
    {

        //Comprobar estados alterados (par, frz, dor)

        bool canRunMovement = attacker.Pokemon.OnStartTurn();
        if (!canRunMovement)
        {
            yield return ShowStatsMessages(attacker.Pokemon);
            yield return attacker.Hud.UpdatePokemonData();
            yield break;
        }


        yield return ShowStatsMessages(attacker.Pokemon);

        move.Pp--;
        yield return battleDialogBox.SetDialog($"{attacker.Pokemon.Base.Name} ha usado {move.Base.Name}.");


        if (MoveHits(move, attacker.Pokemon, target.Pokemon))
        {
            yield return RunMoveAnim(attacker, target);


            if (move.Base.MoveType == MoveType.Stats)
            {
                yield return RunMoveStats(attacker.Pokemon, target.Pokemon, move.Base.Effects, move.Base.Target);
            }
            else
            {
                var damageDesc = target.Pokemon.ReceiveDamage(attacker.Pokemon, move);
                yield return target.Hud.UpdatePokemonData();
                yield return ShowDamageDescription(damageDesc);
            }

            //Chequear posibles estados secundarios

            if (move.Base.SecondaryEffects != null && move.Base.SecondaryEffects.Count > 0)
            {
                foreach (var sec in move.Base.SecondaryEffects)
                {
                    if ((sec.Target == MoveTarget.Other && target.Pokemon.HP > 0) || 
                        (sec.Target == MoveTarget.Me && attacker.Pokemon.HP > 0))
                    {
                        var rnd = Random.Range(0, 99);

                        if (rnd < sec.Chance)
                        {
                            yield return RunMoveStats(attacker.Pokemon, target.Pokemon, sec, sec.Target);
                        }

                    }
                }
            }

            if (target.Pokemon.HP <= 0)
            {
                yield return HandlePokemonFainted(target);
            }
        }
        else
        {
            yield return battleDialogBox.SetDialog($"¡El ataque de {attacker.Pokemon.Base.Name} ha fallado!");
        }


        //Comprobar estados alterados


        attacker.Pokemon.OnFinishTurn();
        yield return ShowStatsMessages(attacker.Pokemon);
        yield return attacker.Hud.UpdatePokemonData();

        if (attacker.Pokemon.HP <= 0)
        {
            yield return HandlePokemonFainted(attacker);
        }
    }



    IEnumerator RunMoveAnim(BattleUnit attacker, BattleUnit target)
    {
        attacker.PlayAttackAnimation();
        SoundManager.SharedInstance.PlaySound(attackClip);
        yield return new WaitForSeconds(1f);

        target.PlayReceiveAttackAnimation();
        SoundManager.SharedInstance.PlaySound(damageClip);
        yield return new WaitForSeconds(1f);
    }



    IEnumerator RunMoveStats(Pokemon attacker, Pokemon target, MoveStatEffect effect, MoveTarget moveTarget)
    {

        //Stat Boost

        foreach (var boost in effect.Boostings)
        {
            if (boost.target == MoveTarget.Me)
            {
                attacker.ApplyBoost(boost);
            }
            else
            {
                target.ApplyBoost(boost);
            }
        }

        //Cambio de estado

        if (effect.Status != StatusConditionID.none)
        {
            if(moveTarget == MoveTarget.Other)
            {
                target.SetConditionStatus(effect.Status);
            }
            else
            {
                attacker.SetConditionStatus(effect.Status);
            }
        }

        //Estado volátil

        if (effect.VolatileStatus != StatusConditionID.none)
        {
            if (moveTarget == MoveTarget.Other)
            {
                target.SetVolatileConditionStatus(effect.VolatileStatus);
            }
            else
            {
                attacker.SetVolatileConditionStatus(effect.VolatileStatus);
            }
        }

        yield return ShowStatsMessages(attacker);
        yield return ShowStatsMessages(target);
    }

    bool MoveHits(Move move, Pokemon attacker, Pokemon target)
    {

        if (move.Base.AlwaysHit)
        {
            return true;
        }
        float rnd = Random.Range(0, 100);
        float moveAcc = move.Base.Accuracy;

        float accuracy = attacker.StatsBoosted[Stat.Accuracy];
        float evasion = attacker.StatsBoosted[Stat.Evasion];

        float multiplierAcc = 1f + Mathf.Abs(accuracy) / 3f;
        float multiplierEva = 1f + Mathf.Abs(evasion) / 3f;


        if (accuracy > 0)
        {
            moveAcc *= multiplierAcc;
        }
        else
        {
            moveAcc /= multiplierAcc;
        }


        if (evasion > 0)
        {
            moveAcc /= multiplierEva;
        }
        else
        {
            moveAcc *= multiplierEva;
        }



        return rnd < moveAcc;
    }
    IEnumerator ShowStatsMessages(Pokemon pokemon)
    {
        while (pokemon.StatusChangeMessages.Count > 0)
        {
            var message = pokemon.StatusChangeMessages.Dequeue();
            yield return battleDialogBox.SetDialog(message);
        }
    }
    void CheckForBattleFinish(BattleUnit faintedUnit)
    {
        if (faintedUnit.IsPlayer)
        {
            var nextPokemon = playerParty.GetFirstNonFaintedPokemon();
            if (nextPokemon != null)
            {
                OpenPartySelectionScreen();
            }
            else
            {
                BattleFinish(false);
            }
        }
        else
        {
            BattleFinish(true);
        }
    }

    IEnumerator ShowDamageDescription(DamageDescription desc)
    {
        if (desc.Critical > 1f)
        {
            yield return battleDialogBox.SetDialog("¡Un golpe crítico!");
        }

        if (desc.Type > 1)
        {
            yield return battleDialogBox.SetDialog("¡Es super efectivo!");
        }
        else if (desc.Type < 1)
        {
            yield return battleDialogBox.SetDialog("No es muy eficaz...");
        }
        else if (desc.Type == 0)
        {
            yield return battleDialogBox.SetDialog("No tuvo efecto...");
        }

    }

    IEnumerator SwitchPokemon(Pokemon newPokemon)
    {

        bool currentPokemonFainted = true;

        if (playerUnit.Pokemon.HP > 0)
        {
            currentPokemonFainted = false;
            yield return battleDialogBox.SetDialog($"¡Vuelve {playerUnit.Pokemon.Base.Name}!");
            playerUnit.PlayFaintAnimation();
            yield return new WaitForSeconds(1.5f);
        }


        playerUnit.SetupPokemon(newPokemon);
        battleDialogBox.SetPokemonMovements(newPokemon.Moves);

        yield return battleDialogBox.SetDialog($"¡Ve {newPokemon.Base.Name}!");
        if (currentPokemonFainted)
        {
            yield return ChooseFirstTurn();
        }
        else
        {
            yield return PerformEnemyMovement();
        }
    }


    IEnumerator ThrowPokeball()
    {
        state = BattleState.Busy;

        if (type != BattleType.WildPokemon)
        {
            yield return battleDialogBox.SetDialog("No puedes robar los pokemon de otros entrenadores");
            state = BattleState.LoseTurn;
            yield break;
        }

        yield return battleDialogBox.SetDialog($"Has lanzado una {pokeball.name}!");

        SoundManager.SharedInstance.PlaySound(pokeballClip);
        var pokeballInst = Instantiate(pokeball, playerUnit.transform.position +
                                                 new Vector3(-2, 0),
                                                  Quaternion.identity);

        var pokeballSpt = pokeballInst.GetComponent<SpriteRenderer>();

        yield return pokeballSpt.transform.DOLocalJump(enemyUnit.transform.position +
                                          new Vector3(0, 1.5f), 2f,
                                         1, 1f).WaitForCompletion();
        yield return enemyUnit.PlayCapturedAnimation();
        yield return pokeballSpt.transform.DOLocalMoveY(enemyUnit.transform.position.y - 2f, 0.3f).WaitForCompletion();

        var numberOfShakes = TryToCatchPokemon(enemyUnit.Pokemon);
        for (int i = 0; i < Mathf.Min(numberOfShakes, 3); i++)
        {
            yield return new WaitForSeconds(0.5f);
            yield return pokeballSpt.transform.DOPunchRotation(new Vector3(0, 0, 15f), 0.6f).WaitForCompletion();
        }

        if (numberOfShakes == 4)
        {
            yield return battleDialogBox.SetDialog($"¡{enemyUnit.Pokemon.Base.Name} capturado!");
            yield return pokeballSpt.DOFade(0, 1.5f).WaitForCompletion();

            if (playerParty.AddPokemonToParty(enemyUnit.Pokemon))
            {
                yield return battleDialogBox.SetDialog($"{enemyUnit.Pokemon.Base.Name} se ha añadido a tu equipo.");
            }
            else
            {
                yield return battleDialogBox.SetDialog("En algun momento, lo mandaremos al PC de Bill...");
            }

            Destroy(pokeballInst);
            BattleFinish(true);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            pokeballSpt.DOFade(0, 0.2f);
            yield return enemyUnit.PlayBreakOutAnimation();

            if (numberOfShakes < 2)
            {
                yield return battleDialogBox.SetDialog($"¡{enemyUnit.Pokemon.Base.Name} ha escapado!");
            }
            else
            {
                yield return battleDialogBox.SetDialog("¡Casi lo has atrapado!");
            }
            Destroy(pokeballInst);
            state = BattleState.LoseTurn;
        }

    }


    int TryToCatchPokemon(Pokemon pokemon)
    {
        float bonusPokeball = 1; // TODO: clase pokeball con su multiplicador
        float bonusStat = 1;     //TODO: stats para chequear condición de modificación
        float a = (3 * pokemon.MaxHP - 2 * pokemon.HP) * pokemon.Base.CatchRate * bonusPokeball * bonusStat / (3 * pokemon.MaxHP);

        if (a >= 255)
        {
            return 4;
        }


        float b = 1048560 / Mathf.Sqrt(Mathf.Sqrt(16711680 / a));

        int shakeCount = 4;
        while (shakeCount < 4)
        {
            if (Random.Range(0, 65535) >= b)
            {
                break;
            }
            else
            {
                shakeCount++;
            }
        }

        return shakeCount;
    }



    IEnumerator TryToEscapeFromBattle()
    {
        state = BattleState.Busy;

        if (type != BattleType.WildPokemon)
        {
            yield return battleDialogBox.SetDialog("No puedes huir de combates contra entrenadores Pokemon");
            state = BattleState.LoseTurn;
            yield break;
        }

        escapeAttempts++;

        //Es contra un Pokemon salvaje
        int playerSpeed = playerUnit.Pokemon.Speed;
        int enemySpeed = enemyUnit.Pokemon.Speed;

        if (playerSpeed >= enemySpeed)
        {
            yield return battleDialogBox.SetDialog("Has escapado con éxito");
            yield return new WaitForSeconds(1);
            OnBattleFinish(true);
        }
        else
        {
            int oddsScape = (Mathf.FloorToInt(playerSpeed * 128 / enemySpeed) + 30 * escapeAttempts) % 256;
            if (Random.Range(0, 256) < oddsScape)
            {
                yield return battleDialogBox.SetDialog("Has escapado con éxito");
                yield return new WaitForSeconds(1);
                OnBattleFinish(true);
            }
            else
            {
                yield return battleDialogBox.SetDialog("No puedes escapar");
                state = BattleState.LoseTurn;
            }
        }

    }

    IEnumerator HandlePokemonFainted(BattleUnit faintedUnit)
    {
        yield return battleDialogBox.SetDialog($"{faintedUnit.Pokemon.Base.Name} se ha debilitado.");
        SoundManager.SharedInstance.PlaySound(faintedClip);
        faintedUnit.PlayFaintAnimation();
        yield return new WaitForSeconds(1.5f);

        if (!faintedUnit.IsPlayer)
        {
            //EXP ++
            int expBase = faintedUnit.Pokemon.Base.ExpBase;
            int level = faintedUnit.Pokemon.Level;
            float multiplier = (type == BattleType.WildPokemon ? 1 : 1.5f);
            int wonExp = Mathf.FloorToInt(expBase * level * multiplier / 7);
            playerUnit.Pokemon.Experience += wonExp;
            yield return battleDialogBox.SetDialog(
               $"{playerUnit.Pokemon.Base.Name} ha ganado {wonExp} puntos de experiencia.");
            yield return playerUnit.Hud.SetExpSmooth();
            yield return new WaitForSeconds(0.5f);

            //Chequear New Level
            while (playerUnit.Pokemon.NeedsToLevelUp())
            {
                SoundManager.SharedInstance.PlaySound(levelUpClip);
                playerUnit.Hud.SetLevelText();
                playerUnit.Pokemon.HasHPChanged = true;
                yield return playerUnit.Hud.UpdatePokemonData();
                yield return new WaitForSeconds(1);
                yield return battleDialogBox.SetDialog($"{playerUnit.Pokemon.Base.Name} sube de nivel!");


                //INTENTAR APRENDER UN NUEVO MOVIMIENTO
                var newLearnableMove = playerUnit.Pokemon.GetLearnableMoveAtCurrentLevel();
                if (newLearnableMove != null)
                {
                    if (playerUnit.Pokemon.Moves.Count < PokemonBase.NUMBER_OF_LEARNABLE_MOVES)
                    {
                        playerUnit.Pokemon.LearnMove(newLearnableMove);
                        yield return battleDialogBox.SetDialog(
                           $"{playerUnit.Pokemon.Base.Name} ha aprendido {newLearnableMove.Move.Name}");
                        battleDialogBox.SetPokemonMovements(playerUnit.Pokemon.Moves);
                    }
                    else
                    {
                        yield return battleDialogBox.SetDialog(
                           $"{playerUnit.Pokemon.Base.Name} intenta aprender {newLearnableMove.Move.Name}");
                        yield return battleDialogBox.SetDialog(
                           $"Pero no puedo aprener más de {PokemonBase.NUMBER_OF_LEARNABLE_MOVES} movimientos");
                        yield return ChooseMovementToForget(playerUnit.Pokemon, newLearnableMove.Move);
                        yield return new WaitUntil(() => state != BattleState.ForgetMovement);
                    }
                }

                yield return playerUnit.Hud.SetExpSmooth(true);
            }
        }

        CheckForBattleFinish(faintedUnit);
    }


    IEnumerator ChooseMovementToForget(Pokemon learner, MoveBase newMove)
    {
        state = BattleState.Busy;
        yield return battleDialogBox.SetDialog("Selecciona el movimiento que quieres olvidar");
        selectMoveUI.gameObject.SetActive(true);
        selectMoveUI.SetMovements(learner.Moves.Select(mv => mv.Base).ToList(), newMove);
        moveToLearn = newMove;
        state = BattleState.ForgetMovement;

    }

}
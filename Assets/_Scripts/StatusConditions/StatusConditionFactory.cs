using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatusConditionFactory
{
    public static void InitFactory()
    {
        foreach (var condition in StatusConditions)
        {
            var id = condition.Key;
            var statusCondition = condition.Value;
            statusCondition.ID = id;
        }
    }
    public static Dictionary<StatusConditionID, StatusCondition> StatusConditions { get; set; } = new Dictionary<StatusConditionID, StatusCondition>()
        {
            {
                StatusConditionID.psn,
                new StatusCondition()
                {
                    Name = "Poison",
                    Description = "Hace que el pokemon sufra daño en cada turno",
                    StartMessage = "ha sido envenenado",

                    OnFinishTurn = PoisonEffect
                }
            },
            {
                StatusConditionID.brn,
                new StatusCondition()
                {
                    Name = "Burn",
                    Description = "Hace que el pokemon sufra daño en cada turno",
                    StartMessage = "ha sido quemado",

                    OnFinishTurn = BurnEffect
                }
            },
            {
                StatusConditionID.par,
                new StatusCondition()
                {
                    Name = "Paralyzed",
                    Description = "Hace que el pokemon pueda quedar paralizado",
                    StartMessage = "ha sido paralizado",

                    OnStartTurn = ParalyzedEffect
                }
            },
            {
                StatusConditionID.frz,
                new StatusCondition()
                {
                    Name = "Frozen",
                    Description = "Hace que el pokemon se quede congelado y no ataque, pero se cura aleatoriamente",
                    StartMessage = "ha sido congelado",

                    OnStartTurn = FrozenEffect
                }
            },
            {
                StatusConditionID.slp,
                new StatusCondition()
                {
                    Name = "Sleep",
                    Description = "Hace que el pokemon se quede dormido y no ataque, pero se cura al cabo de un numero fijo de turnos",
                    StartMessage = "está dormido...",

                    OnApplyStatusCondition = (Pokemon pokemon) =>
                    {
                        pokemon.StatusNumberTurns = Random.Range(2, 6);
                        Debug.Log($"Al pokemon le quedan {pokemon.StatusNumberTurns} turnos dormido");
                    },
                    
                    OnStartTurn = (Pokemon pokemon) =>
                    {
                        if (pokemon.StatusNumberTurns <= 0)
                        {
                            pokemon.CureStatusCondition();
                            pokemon.StatusChangeMessages.Enqueue($"¡{pokemon.Base.Name} por fin abre los ojos!");
                            return true;
                        }

                        pokemon.StatusNumberTurns--;
                        pokemon.StatusChangeMessages.Enqueue($"¡{pokemon.Base.Name} está como un tronco!");
                        return false;
                    }
                }
            },
            //Estados volátiles
            {
                StatusConditionID.conf,
                new StatusCondition()
                {
                    Name = "Confused",
                    Description = "Hace que el pokemon este confuso y se pueda atacar a sí mismo",
                    StartMessage = "está confuso",

                    OnApplyStatusCondition = (Pokemon pokemon) =>
                    {
                        pokemon.VolatileStatusNumberTurns = Random.Range(2, 6);
                        Debug.Log($"Al pokemon le quedan {pokemon.VolatileStatusNumberTurns} turnos confuso");
                    },

                    OnStartTurn = (Pokemon pokemon) =>
                    {
                        if (pokemon.VolatileStatusNumberTurns <= 0)
                        {
                            pokemon.CureVolatileStatusCondition();
                            pokemon.StatusChangeMessages.Enqueue($"¡{pokemon.Base.Name} ya no está confuso!");
                            return true;
                        }

                        pokemon.VolatileStatusNumberTurns--;
                        pokemon.StatusChangeMessages.Enqueue($"¡{pokemon.Base.Name} no sabe que hacer!");

                        if (Random.RandomRange(0,2) == 0)
                        {
                            return true;
                        }
                        //Nos hacemos daño
                        pokemon.UpdateHP(pokemon.MaxHP/6);
                        pokemon.StatusChangeMessages.Enqueue($"¡{pokemon.Base.Name} está tan confuso que se hiere a sí mismo!");
                        return false;
                    }
                }
            }
        };

    static void PoisonEffect(Pokemon pokemon)
    {

        pokemon.UpdateHP(Mathf.CeilToInt((float)pokemon.MaxHP / 8));
        pokemon.StatusChangeMessages.Enqueue($"{pokemon.Base.Name} sigue envenenado");
    }

    static void BurnEffect(Pokemon pokemon)
    {

        pokemon.UpdateHP(Mathf.CeilToInt((float)pokemon.MaxHP / 15));
        pokemon.StatusChangeMessages.Enqueue($"Se está haciendo una barbacoa de {pokemon.Base.Name}...");
    }

    static bool ParalyzedEffect(Pokemon pokemon)
    {
        if (Random.Range(0, 100) <25)
        {
            pokemon.StatusChangeMessages.Enqueue($"¡{pokemon.Base.Name} está paralizado!");
            return false;
        }
        return true;
    }

    static bool FrozenEffect(Pokemon pokemon)
    {
        if (Random.Range(0, 100) < 25)
        {
            pokemon.CureStatusCondition();
            pokemon.StatusChangeMessages.Enqueue($"¡El hielo de {pokemon.Base.Name} se ha derretido!");
            return true;
        }
        pokemon.StatusChangeMessages.Enqueue($"¡{pokemon.Base.Name} sigue congelado!");
        return false;
    }
}

public enum StatusConditionID
{
    none, brn, frz, par, psn, slp, conf
}

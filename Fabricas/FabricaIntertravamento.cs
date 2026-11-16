using System;
using System.Collections.Generic;
using System.Linq;

namespace Genesis.Fabricas
{
    internal static class FabricaIntertravamento
    {
        /// <summary>Condição de nível baixo (LEI_BAI).</summary>
        public static List<string> CondicoesPorNivelBaixo(string eq, string fn)
        {
            return new List<string> { $"SEN.{eq}.{fn}.LEI_BAI" };
        }

        /// <summary>Condição de nível alto (LEI_ALT).</summary>
        public static List<string> CondicoesPorNivelAlto(string eq, string fn)
        {
            return new List<string> { $"SEN.{eq}.{fn}.LEI_ALT" };
        }

        /// <summary>Condição do motor ligado (NÃO LEI_RUN ou NÃO LEI_HAB, dependendo do tipo).</summary>
        public static List<string> CondicoesPorMotorLigado(string sub, string eq, string tipoMotor)
        {
            string tag = tipoMotor.Contains("rede") ? $"{sub}.{eq}.MOT.LEI_RUN" : $"{sub}.{eq}.MOT.LEI_HAB";
            return new List<string> { "NÃO " + tag };
        }

        /// <summary>Junta as condições em uma única string (uma por linha).</summary>
        public static string JuntarCondicoes(List<string> condicoes)
        {
            return string.Join("\n", condicoes);
        }

        /// <summary>
        /// Intertravamento padrão da Bomba de Alimentação:
        ///  - Nível Alto Lodo (se existir)
        ///  - Nível Baixo Alimentação (se existir)
        ///  - Motor do Adensador (LEI_RUN ou LEI_HAB negado)
        /// </summary>
        public static void LigarBombaAlimentacao(Partida bomba, string eqNivAlim, string fnNivAlim, string eqNivLad, string fnNivLad, string subMot, string eqMot, string tipoMotor)
        {
            var conds = new List<string>();
            
            if (!string.IsNullOrEmpty(eqNivLad) && !string.IsNullOrEmpty(fnNivLad))
                conds.AddRange(CondicoesPorNivelAlto(eqNivLad, fnNivLad));
            
            if (!string.IsNullOrEmpty(eqNivAlim) && !string.IsNullOrEmpty(fnNivAlim))
                conds.AddRange(CondicoesPorNivelBaixo(eqNivAlim, fnNivAlim));
            
            conds.AddRange(CondicoesPorMotorLigado(subMot, eqMot, tipoMotor));
            
            bomba.Intertravamento = true;
            bomba.CondicoesInt = JuntarCondicoes(conds);
        }

        /// <summary>
        /// Intertravamento padrão da Bomba de Floculante:
        ///  - Nível Baixo Floculante (se existir)
        ///  - Bomba de Alimentação (LEI_RUN ou LEI_HAB negado)
        /// </summary>
        public static void LigarBombaFloculante(Partida bomba, string eqNivFlc, string fnNivFlc, string subAlim, string eqAlim, string tipoMotorAlim)
        {
            var conds = new List<string>();
            
            if (!string.IsNullOrEmpty(eqNivFlc) && !string.IsNullOrEmpty(fnNivFlc))
                conds.AddRange(CondicoesPorNivelBaixo(eqNivFlc, fnNivFlc));
            
            string tagAlim = tipoMotorAlim.Contains("rede") ? $"{subAlim}.ALM.BBA.LEI_RUN" : $"{subAlim}.ALM.BBA.LEI_HAB";
            conds.Add("NÃO " + tagAlim);
            
            bomba.Intertravamento = true;
            bomba.CondicoesInt = JuntarCondicoes(conds);
        }
    }
}
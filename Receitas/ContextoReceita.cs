using System;
using System.Collections.Generic;
using System.Linq;
using Genesis.Hardware;

namespace Genesis.Receitas
{
    /// <summary>
    /// Contexto passado a uma receita: tudo que ela precisa saber sobre o projeto
    /// para decidir o que gerar. É somente leitura — a receita não altera nada aqui.
    ///
    /// Instâncias são criadas pelo MainForm no momento de criar/reconfigurar uma máquina.
    /// </summary>
    internal class ContextoReceita
    {
        /// <summary>Todas as linhas do projeto (partidas + sensores + PIDs), já preenchidas.</summary>
        public IList<Partida> TodasAsLinhas { get; private set; }

        /// <summary>Hardware do projeto (pode ser null em projetos antigos, antes da Fase B estar em uso).</summary>
        public ProjetoHardware Hardware { get; private set; }

        public ContextoReceita(IList<Partida> todasAsLinhas, ProjetoHardware hardware)
        {
            TodasAsLinhas = todasAsLinhas ?? new List<Partida>();
            Hardware = hardware;
        }

        // ===================================================================
        //  Consultas sobre o hardware
        // ===================================================================

        /// <summary>
        /// O projeto está configurado para rede (PROFINET) ou terminais (fiação direta)?
        /// Se o hardware não foi configurado ainda, assume terminais (comportamento padrão antigo).
        /// </summary>
        public bool ViaRede
        {
            get { return Hardware != null && Hardware.ViaRede; }
        }

        /// <summary>Prefixos de bloco configurados (ex.: "G_"). Vazio = sem prefixo.</summary>
        public string Prefixo
        {
            get { return Cfg.Prefixo ?? ""; }
        }

        // ===================================================================
        //  Consultas sobre linhas existentes
        // ===================================================================

        /// <summary>Todos os ids de máquina em uso no projeto (o campo Maquina de qualquer linha).</summary>
        public IEnumerable<string> IdsDeMaquinaEmUso()
        {
            return TodasAsLinhas
                .Where(x => !string.IsNullOrEmpty(x.Maquina))
                .Select(x => x.Maquina)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>true se o id (ex.: "FL1") já está sendo usado como máquina em alguma linha.</summary>
        public bool IdEmUso(string id)
        {
            return !string.IsNullOrEmpty(id)
                && TodasAsLinhas.Any(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Próximo id livre com o prefixo dado (ex.: "FL" → "FL1", "FL2"...).</summary>
        public string ProximoIdLivre(string prefixo)
        {
            int n = 1;
            while (IdEmUso(prefixo + n)) n++;
            return prefixo + n;
        }

        /// <summary>Linhas que pertencem à máquina com o id dado.</summary>
        public IEnumerable<Partida> LinhasDaMaquina(string id)
        {
            return TodasAsLinhas.Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>true se já existe uma linha com Sub/Eq/Fn iguais.</summary>
        public bool LinhaExiste(string sub, string eq, string fn)
        {
            return TodasAsLinhas.Any(x =>
                string.Equals(x.Sub, sub, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Eq, eq, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Fn, fn, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Um sensor de nível do tanque de um químico/preparador já existe no projeto?
        /// Usado pelo Flotador para não recriar "SEN.ALC.TBA" quando o ALCalinizante já foi criado antes.
        /// </summary>
        public Partida SensorDeNivelExistente(string sub, string eq, bool analogico)
        {
            string fn = analogico ? "TAN" : "TBA";
            return TodasAsLinhas.FirstOrDefault(x =>
                string.Equals(x.Sub, sub, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Eq, eq, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Fn, fn, StringComparison.OrdinalIgnoreCase));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Genesis
{
    // ===================================================================
    //  Página "Gerar": escolhe o que gerar, confere o projeto e dispara a geração
    // ===================================================================
    internal partial class MainForm
    {
        private enum Escopo { Tudo, Marcados, Modbus, Maquina }

        private Escopo _escopo = Escopo.Tudo;
        private OpcaoCartao _opTudo, _opMarcados, _opModbus, _opMaquina;
        private Panel _listaConferencia;
        private Label _lblResultado, _lblPorque;
        private Button _btnGerarAgora;
        private Panel _colunaGerar;
        private Cartao _cardEscopo;
        private Label _tituloEscopo;
        private bool _conferindo;

        private Control CriarPaginaGerar()
        {
            var grade = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = Tema.Fundo };
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            grade.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // --- esquerda: o que gerar + botão (posições calculadas em ArrumarGerar) ---
            _colunaGerar = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Fundo, Margin = new Padding(0, 0, 14, 0) };
            Win.Escurecer(_colunaGerar);
            _cardEscopo = new Cartao { BarraAzul = true, Padding = new Padding(0) };
            _tituloEscopo = Tema.TituloCartao("1 · O que gerar");
            _tituloEscopo.Location = new Point(22, 16);
            _cardEscopo.Controls.Add(_tituloEscopo);

            _opTudo = new OpcaoCartao("Tudo",
                "Recria o DB, a FC e as instâncias de cada subsistema no grupo \"" + Gerador.NomeGrupo + "\" e, se estiver ligado, o mapa Modbus.")
            { Aviso = "Edições manuais feitas nesses blocos no TIA (ex.: intertravamento preenchido à mão) são perdidas." };
            _opMarcados = new OpcaoCartao("Só os itens marcados",
                "Acrescenta só o que está com a caixinha GERAR nas listas. A FC que já existe não é alterada: as redes vão para uma FC nova (…_NOVAS) "
                + "para você copiar. No DB entra só o que falta.");
            _opMaquina = new OpcaoCartao("Só a máquina", "") { Visible = false };
            _opModbus = new OpcaoCartao("Só a comunicação Modbus",
                "Recria o DB do mapa com os registros atuais e refaz a FC de comando remoto. As partidas não são alteradas.")
            { Aviso = "Se o mapa mudou, avise a equipe do supervisório." };
            var opcoes = new[] { _opTudo, _opMarcados, _opMaquina, _opModbus };
            var escopos = new[] { Escopo.Tudo, Escopo.Marcados, Escopo.Maquina, Escopo.Modbus };
            for (int i = 0; i < opcoes.Length; i++)
            {
                Escopo e = escopos[i];
                opcoes[i].Escolhida += delegate { _escopo = e; MostrarResultado(null, null); AtualizarConferencia(); };
                _cardEscopo.Controls.Add(opcoes[i]);
            }

            _btnGerarAgora = Tema.Botao("Gerar no TIA Portal", true, delegate { GerarAgora(); }, "gerar");
            _btnGerarAgora.AutoSize = false;
            _btnGerarAgora.Font = Tema.Fonte(10.5f, FontStyle.Bold);
            _lblPorque = new Label { AutoSize = false, ForeColor = Tema.TextoSec, BackColor = Tema.Fundo, Font = Tema.Pequena, UseMnemonic = false };
            _lblResultado = new Label { AutoSize = false, AutoEllipsis = true, BackColor = Tema.Fundo, Font = Tema.Fonte(9.75f, FontStyle.Bold), UseMnemonic = false, Visible = false };
            _colunaGerar.Controls.Add(_cardEscopo);
            _colunaGerar.Controls.Add(_btnGerarAgora);
            _colunaGerar.Controls.Add(_lblPorque);
            _colunaGerar.Controls.Add(_lblResultado);
            _colunaGerar.Resize += delegate { ArrumarGerar(); };

            // --- direita: conferência ---
            var dir = new Cartao { Dock = DockStyle.Fill, BarraAzul = true, Padding = new Padding(22, 16, 10, 12), Margin = new Padding(0) };
            var topo = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.Transparent };
            Label tConf = Tema.TituloCartao("2 · Conferência");
            tConf.Location = new Point(0, 0);
            Label sConf = Tema.Ajuda("Feita agora, com o que está cadastrado. Clique numa linha para ir até o item.");
            sConf.Location = new Point(1, 26);
            Button atualizar = Tema.Botao("Conferir de novo", false, delegate { AtualizarConferencia(); });
            atualizar.AutoSize = false;
            atualizar.Size = new Size(140, 38);
            topo.Controls.Add(tConf);
            topo.Controls.Add(sConf);
            topo.Controls.Add(atualizar);
            topo.Resize += delegate { atualizar.Location = new Point(topo.Width - atualizar.Width - 12, 2); };
            _listaConferencia = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Cartao, Padding = new Padding(0, 0, 8, 0) };
            Win.Escurecer(_listaConferencia);
            dir.Controls.Add(_listaConferencia);
            dir.Controls.Add(topo);

            grade.Controls.Add(_colunaGerar, 0, 0);
            grade.Controls.Add(dir, 1, 0);

            return NovaPagina("Gerar no TIA Portal", "Escolha o que gerar, confira e dispare",
                null, null, grade);
        }

        /// <summary>Posiciona a coluna da esquerda: opções (só as visíveis), botão e mensagens.</summary>
        private void ArrumarGerar()
        {
            if (_colunaGerar == null || _cardEscopo == null) return;
            // largura já descontando a barra de rolagem (que aparece quando o log abre): não muda ao rolar
            int larg = Math.Max(300, _colunaGerar.Width - SystemInformation.VerticalScrollBarWidth - 4);
            int dx = _colunaGerar.AutoScrollPosition.X, dy = _colunaGerar.AutoScrollPosition.Y;
            int y = 16 + 22 + 14;
            foreach (OpcaoCartao o in new[] { _opTudo, _opMarcados, _opMaquina, _opModbus })
            {
                if (!o.Visible) continue;
                o.Width = larg - 22 - 18;
                o.Ajustar();
                o.Location = new Point(22, y);
                y += o.Height + 8;
            }
            _cardEscopo.SetBounds(dx, dy, larg, y + 10);
            y = _cardEscopo.Height + 14;
            if (_lblResultado.Visible) { _lblResultado.SetBounds(dx + 2, dy + y - 4, larg - 4, 26); y += 30; }
            _btnGerarAgora.SetBounds(dx, dy + y, larg, 52); y += 52 + 8;
            _lblPorque.SetBounds(dx + 2, dy + y, larg - 4, 20);
        }

        /// <summary>Abre a página Gerar (escopo null = mantém o último; vindo do menu, nunca fica preso numa máquina).</summary>
        private void AbrirGerar(Escopo? escopo)
        {
            FinalizarEdicao();
            if (escopo.HasValue) _escopo = escopo.Value;
            else if (_escopo == Escopo.Maquina) _escopo = Escopo.Tudo;
            MostrarResultado(null, null);
            if (_paginaAtual == _pagGerar) AtualizarConferencia();
            else MostrarPagina(_pagGerar);
        }

        private void GerarAgora()
        {
            switch (_escopo)
            {
                case Escopo.Marcados: Gerar(true, false, null); break;
                case Escopo.Modbus: Gerar(false, true, null); break;
                case Escopo.Maquina:
                    Gerar(true, false, ItensDaMaquina(_maqAtual).Where(x => !PartidaVazia(x)).ToList());
                    break;
                default: Gerar(false, false, null); break;
            }
        }

        /// <summary>Faixa de resultado embaixo do botão (ok = null: mensagem neutra; texto = null: esconde).</summary>
        private void MostrarResultado(bool? ok, string texto)
        {
            if (_lblResultado == null) return;
            _lblResultado.Visible = !string.IsNullOrEmpty(texto);
            _lblResultado.Text = texto ?? "";
            _lblResultado.ForeColor = ok == true ? Tema.Verde : ok == false ? Tema.Vermelho : Tema.Ciano;
            ArrumarGerar();
        }

        private static int JanelasDoTia()
        {
            try { return System.Diagnostics.Process.GetProcessesByName("Siemens.Automation.Portal").Length; }
            catch { return -1; }
        }

        private void IrParaItem(Partida p)
        {
            Cadastro cad = CadDe(p);
            if (!cad.Lista.Contains(p)) return;
            cad.Busca.Text = "";
            if (cad.Filtro.SelectedIndex != 0) cad.Filtro.SelectedIndex = 0;
            MostrarPagina(cad == _cadPart ? _pagPartidas : cad == _cadSen ? _pagSensores : _pagPids);
            Selecionar(cad, p);
            MontarEditor(cad, true);
        }

        private void IrParaTag(int indice)
        {
            MostrarPagina(_pagTags);
            if (indice < 0 || indice >= _gridIos.Rows.Count) return;
            _gridIos.ClearSelection();
            _gridIos.CurrentCell = _gridIos.Rows[indice].Cells[0];
            _gridIos.Rows[indice].Selected = true;
        }

        /// <summary>Ação de uma mensagem da validação ("Aba Partidas, linha 3: ..."): leva ao item citado.</summary>
        private Action AcaoDoErro(string erro)
        {
            Match m = Regex.Match(erro ?? "", @"^Aba (IOs|Partidas|Sensores|PIDs), linha (\d+)");
            if (!m.Success)
            {
                if ((erro ?? "").StartsWith("Aba IOs")) return delegate { MostrarPagina(_pagTags); };
                return null;
            }
            int i = int.Parse(m.Groups[2].Value) - 1;
            string aba = m.Groups[1].Value;
            if (aba == "IOs") return delegate { IrParaTag(i); };
            BindingListDe lista = aba == "Partidas" ? (BindingListDe)(() => _partidas) : aba == "Sensores" ? (BindingListDe)(() => _sensores) : () => _pids;
            return delegate
            {
                var l = lista();
                if (i >= 0 && i < l.Count) IrParaItem(l[i]);
            };
        }

        private delegate System.ComponentModel.BindingList<Partida> BindingListDe();

        private static string TextoDoErro(string erro)
        {
            // "Aba Partidas, linha 3: ..." -> "Partidas, linha 3: ..." (as abas viraram páginas)
            string t = (erro ?? "").StartsWith("Aba ") ? erro.Substring(4) : (erro ?? "");
            return t.StartsWith("IOs") ? "Tags" + t.Substring(3) : t;
        }

        private static Label TituloDaConferencia(string texto)
        {
            Label l = Tema.Secao(texto);
            l.AutoSize = false;
            l.Dock = DockStyle.Top;
            l.Height = 30;
            l.TextAlign = ContentAlignment.BottomLeft;
            l.Padding = new Padding(8, 0, 0, 5);
            l.ForeColor = Tema.TextoFraco;
            return l;
        }

        /// <summary>Refaz a conferência: ambiente, cadastro, pendências, validação e o que será gerado.</summary>
        private void AtualizarConferencia()
        {
            if (_listaConferencia == null || _conferindo || _cadPid == null) return;
            _conferindo = true;
            try
            {
                LerConfigDaTela();
                for (int i = 0; i < _partidas.Count; i++) { _partidas[i].Linha = i + 1; _partidas[i].Aba = "Partidas"; }
                for (int i = 0; i < _sensores.Count; i++) { _sensores[i].Linha = i + 1; _sensores[i].Aba = "Sensores"; }
                for (int i = 0; i < _pids.Count; i++) { _pids[i].Linha = i + 1; _pids[i].Aba = "PIDs"; }
                for (int i = 0; i < _ios.Count; i++) _ios[i].Linha = i + 1;
                List<Partida> itens = _partidas.Concat(_sensores).Concat(_pids).Where(x => !PartidaVazia(x)).ToList();
                List<Io> ios = _ios.Where(x => !IoVazia(x)).ToList();

                // --- opções da esquerda ---
                List<Partida> daMaquina = _maqAtual != null ? ItensDaMaquina(_maqAtual).Where(x => !PartidaVazia(x)).ToList() : new List<Partida>();
                if (_escopo == Escopo.Maquina && daMaquina.Count == 0) _escopo = Escopo.Tudo;
                List<Partida> marcados = PartidasSelecionadas(itens);
                _opMaquina.Visible = daMaquina.Count > 0;
                _opMaquina.Text = "Só a máquina " + _maqAtual;
                _opMaquina.Descricao = "Gera " + Plural(daMaquina.Count, "item", "itens") + " desta máquina, do mesmo jeito que os itens marcados. O resto do projeto é ignorado.";
                _opMarcados.Aviso = marcados.Count == 0 ? "Nenhum item marcado: marque a caixinha GERAR nas listas de partidas, sensores ou PIDs."
                    : Plural(marcados.Count, "item marcado", "itens marcados") + ".";
                _opTudo.Marcada = _escopo == Escopo.Tudo;
                _opMarcados.Marcada = _escopo == Escopo.Marcados;
                _opMaquina.Marcada = _escopo == Escopo.Maquina;
                _opModbus.Marcada = _escopo == Escopo.Modbus;
                ArrumarGerar();
                foreach (OpcaoCartao o in new[] { _opTudo, _opMarcados, _opMaquina, _opModbus }) o.Invalidate();

                List<Partida> alvo = _escopo == Escopo.Marcados ? marcados : _escopo == Escopo.Maquina ? daMaquina : itens;
                var linhas = new List<Control>();

                // --- ambiente ---
                linhas.Add(TituloDaConferencia("Ambiente"));
                int tia = JanelasDoTia();
                if (tia > 1)
                    linhas.Add(new LinhaConferencia("alerta", Tema.Amarelo, "TIA Portal aberto em " + tia + " janelas",
                        "A geração usa a primeira que encontrar. Deixe aberta só a do projeto certo.", null));
                else if (tia == 1)
                    linhas.Add(new LinhaConferencia("ok", Tema.Verde, "TIA Portal aberto", "A geração vai para o projeto aberto nele.", null));
                else
                    linhas.Add(new LinhaConferencia("alerta", Tema.Amarelo, "Não encontrei o TIA Portal aberto",
                        "Abra o projeto no TIA Portal antes de gerar.", null));

                // --- cadastro ---
                linhas.Add(TituloDaConferencia("Projeto"));
                int nPart = itens.Count(x => x.Categoria == "Partida"), nSen = itens.Count(x => x.Categoria == "Sensor"), nPid = itens.Count(x => x.Categoria == "PID");
                List<string> subs = itens.Select(x => (x.Sub ?? "").Trim()).Where(x => x.Length > 0).Distinct().ToList();
                if (itens.Count == 0)
                    linhas.Add(new LinhaConferencia("alerta", Tema.Amarelo, "Nada cadastrado ainda",
                        "Cadastre partidas, sensores ou PIDs — ou comece por uma máquina do catálogo.", delegate { MostrarPagina(_pagMaquinas); }));
                else
                    linhas.Add(new LinhaConferencia("ok", Tema.Verde,
                        Plural(nPart, "partida", "partidas") + ", " + Plural(nSen, "sensor", "sensores") + " e " + Plural(nPid, "PID", "PIDs"),
                        Plural(subs.Count, "subsistema", "subsistemas") + ": " + string.Join(", ", subs.ToArray()), null));

                // --- pendências (campos obrigatórios em branco) ---
                var pendentes = alvo.Where(x => Resumo(CadDe(x), x).StartsWith("⚠")).ToList();
                if (alvo.Count > 0 && pendentes.Count == 0)
                    linhas.Add(new LinhaConferencia("ok", Tema.Verde, "Campos obrigatórios preenchidos", null, null));
                else if (pendentes.Count > 0)
                {
                    linhas.Add(new LinhaConferencia("alerta", Tema.Amarelo, Plural(pendentes.Count, "item com campos a preencher", "itens com campos a preencher"),
                        "Não impede a geração, mas a rede sai incompleta. Clique para completar.", null));
                    foreach (Partida p in pendentes.Take(60))
                    {
                        Partida alvoItem = p;
                        string r = Resumo(CadDe(p), p);
                        linhas.Add(new LinhaConferencia("alerta", Tema.Amarelo, Ref(p) + "   —   " + r.TrimStart('⚠', ' '), null, delegate { IrParaItem(alvoItem); }, true));
                    }
                    if (pendentes.Count > 60)
                        linhas.Add(new LinhaConferencia("alerta", Tema.Amarelo, "… e mais " + (pendentes.Count - 60) + " (use o filtro \"Só pendentes\" nas listas)", null, null, true));
                }

                // --- validação (o que impede a geração) ---
                var erros = new List<string>();
                if (!Regex.IsMatch(Cfg.Prefixo ?? "", "^[A-Za-z0-9_]*$")) erros.Add("Configuração: o prefixo dos blocos só pode ter letras, números e _.");
                if (itens.Count > 0)
                {
                    foreach (Partida p in itens) { p.Sub = (p.Sub ?? "").Trim(); p.Eq = (p.Eq ?? "").Trim(); p.Fn = (p.Fn ?? "").Trim(); }
                    erros.AddRange(Gerador.Validar(ios, itens));
                }
                if (erros.Count == 0 && itens.Count > 0)
                    linhas.Add(new LinhaConferencia("ok", Tema.Verde, "Nomes e regras conferidos", "Nenhum problema que impeça a geração.", null));
                else if (erros.Count > 0)
                {
                    linhas.Add(new LinhaConferencia("erro", Tema.Vermelho, Plural(erros.Count, "problema impede a geração", "problemas impedem a geração"),
                        "Corrija antes de gerar. Clique para ir até o item.", null));
                    foreach (string e in erros.Take(80))
                    {
                        Action acao = e.StartsWith("Configuração") ? (Action)delegate { MostrarPagina(_pagConfig); } : AcaoDoErro(e);
                        linhas.Add(new LinhaConferencia("erro", Tema.Vermelho, TextoDoErro(e), null, acao, true));
                    }
                }

                // --- tags ---
                int semEnd = ios.Count(x => !Vazio(x.Nome) && Vazio(x.Endereco));
                if (semEnd > 0)
                    linhas.Add(new LinhaConferencia("info", Tema.Ciano, Plural(semEnd, "tag sem endereço", "tags sem endereço"),
                        "Não são criadas no PLC até terem endereço (as redes usam o nome mesmo assim).", delegate { MostrarPagina(_pagTags); }));
                else if (ios.Count > 0)
                    linhas.Add(new LinhaConferencia("ok", Tema.Verde, Plural(ios.Count, "tag com endereço", "tags com endereço"), null, null));

                // --- o que será gerado ---
                if (itens.Count > 0)
                {
                    linhas.Add(TituloDaConferencia("O que será gerado"));
                    if (_escopo == Escopo.Modbus)
                    {
                        MapaModbus m = MapaAtual();
                        linhas.Add(new LinhaConferencia("blocos", Tema.Ciano, "DB " + Cfg.ComDb + " (DB" + Cfg.ComNumero + ") e FC " + Cfg.Prefixo + "COMANDO_REMOTO",
                            m.TotalRegistros + " registros" + (m.ForaDoPadrao ? "  ·  ⚠ fora do padrão 40001–40110" : "  ·  dentro do padrão 40001–40110"),
                            delegate { MostrarPagina(_pagModbus); }));
                    }
                    else
                    {
                        bool parcial = _escopo != Escopo.Tudo;
                        foreach (var g in alvo.GroupBy(x => x.Sub).Where(grupo => !string.IsNullOrEmpty(grupo.Key)))
                        {
                            var todosDoSub = itens.Where(x => x.Sub == g.Key).ToList();
                            var blocos = new List<string> { "DB " + Cfg.Prefixo + g.Key };
                            if (g.Any(x => !Gerador.EhPid(x))) blocos.Add("FC " + Gerador.NomeFcDe(g.Key, todosDoSub) + (parcial ? "_NOVAS" : ""));
                            if (g.Any(Gerador.EhPid)) blocos.Add("FC " + Gerador.NomeFcPidDe(g.Key) + " (chamar no OB cíclico)");
                            linhas.Add(new LinhaConferencia("blocos", Tema.Ciano, g.Key + "   ·   " + Plural(g.Count(), "item", "itens"),
                                string.Join("   ·   ", blocos.ToArray()) + "   ·   instâncias i" + Cfg.Prefixo + g.Key + "_…", null));
                        }
                        if (!parcial && Cfg.ComAtivo)
                        {
                            MapaModbus m = MapaAtual();
                            linhas.Add(new LinhaConferencia(m.ForaDoPadrao ? "alerta" : "blocos", m.ForaDoPadrao ? Tema.Amarelo : Tema.Ciano,
                                "Mapa Modbus   ·   " + m.TotalRegistros + " registros",
                                "DB " + Cfg.ComDb + " (DB" + Cfg.ComNumero + ")" + (m.ForaDoPadrao ? "   ·   ⚠ passou do padrão 40001–40110" : "   ·   dentro do padrão 40001–40110"),
                                delegate { MostrarPagina(_pagModbus); }));
                        }
                    }
                }

                // --- monta a lista (Dock Top empilha de baixo para cima) ---
                _listaConferencia.SuspendLayout();
                foreach (Control c in _listaConferencia.Controls.Cast<Control>().ToList()) c.Dispose();
                _listaConferencia.Controls.Clear();
                for (int i = linhas.Count - 1; i >= 0; i--) _listaConferencia.Controls.Add(linhas[i]);
                _listaConferencia.ResumeLayout(true);

                // --- botão ---
                string porque = "";
                if (itens.Count == 0) porque = "Cadastre pelo menos uma partida, sensor ou PID.";
                else if (erros.Count > 0) porque = "Corrija os problemas em vermelho na conferência.";
                else if (_escopo == Escopo.Marcados && marcados.Count == 0) porque = "Marque a caixinha GERAR dos itens que quer gerar.";
                _btnGerarAgora.Enabled = porque.Length == 0;
                _btnGerarAgora.Text = _escopo == Escopo.Marcados ? "Gerar " + Plural(marcados.Count, "item marcado", "itens marcados")
                    : _escopo == Escopo.Modbus ? "Gerar a comunicação Modbus"
                    : _escopo == Escopo.Maquina ? "Gerar a máquina " + _maqAtual
                    : "Gerar tudo no TIA Portal";
                _lblPorque.Text = porque.Length > 0 ? porque : "Antes de gerar, o " + App.Nome + " ainda pede uma confirmação.";
                _lblPorque.ForeColor = porque.Length > 0 ? Tema.Amarelo : Tema.TextoFraco;
            }
            finally
            {
                _conferindo = false;
            }
        }
    }
}

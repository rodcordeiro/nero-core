# Packs complementares; Core independente

People CRM e Content Factory entram como Packs fora do Nero. O Core só importa primitivos genéricos (confiança, captura, evidência, drift) e permanece entregável sem nenhum Pack instalado.

**Considered Options**: incluir os fluxos no canônico; copiar o plugin monolítico do COG; Packs separados que consomem o Core.

**Consequences**: Schema permanece fechado (sem `Person` nem tipo editorial); Packs não adicionam tools `nero_*`; o backlog do Core não espera CRM nem factory para avançar.
